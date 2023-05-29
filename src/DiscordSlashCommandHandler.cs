using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OoLunar.CookieClicker.Attributes;
using OoLunar.CookieClicker.Entities.CommandFramework;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker
{
    public sealed class DiscordSlashCommandHandler : IDisposable
    {
        public FrozenDictionary<string, ImmutableCommand> Commands { get; private set; } = FrozenDictionary<string, ImmutableCommand>.Empty;

        private readonly string _token;
        private readonly Snowflake _applicationId;
        private readonly ILogger<DiscordSlashCommandHandler> _logger;
        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private IServiceProvider? _serviceProvider { get; set; }

        [SuppressMessage("Roslyn", "IDE0045", Justification = "Ternary operator rabbit hole.")]
        public DiscordSlashCommandHandler(IConfiguration configuration, ILogger<DiscordSlashCommandHandler> logger, IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions, HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

            if (configuration.GetValue<string>("Discord:ApplicationId") is not string stringApplicationId)
            {
                throw new ArgumentException("Discord application id is not specified.");
            }
            else if (!DiscordSnowflake.TryParse(stringApplicationId, out Snowflake? applicationId))
            {
                throw new ArgumentException("Discord application id is invalid.");
            }
            else
            {
                _applicationId = applicationId.Value;
            }

            _token = configuration["Discord:Token"] ?? throw new ArgumentException("Discord token is not specified.");
            _logger = logger;
            _jsonSerializerOptions = jsonSerializerOptions.Get("Discord");
            _httpClient = httpClient;
        }

        [MemberNotNull(nameof(_serviceProvider))]
        public async Task RegisterAsync(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            List<ImmutableCommand> localCommands = new();
            foreach (Type type in typeof(DiscordSlashCommandHandler).Assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.GetCustomAttribute<CommandAttribute>() is not CommandAttribute commandAttribute)
                {
                    continue;
                }

                localCommands.Add(new ImmutableCommand(commandAttribute, type, serviceProvider));
            }

            HttpRequestMessage request = new(HttpMethod.Put, $"https://discord.com/api/v10/applications/{_applicationId}/commands")
            {
                Content = JsonContent.Create(localCommands.Select(command => (BulkApplicationCommandData)command).ToArray(), typeof(BulkApplicationCommandData[]), MediaTypeHeaderValue.Parse("application/json"), _jsonSerializerOptions)
            };
            request.Headers.Add("Authorization", $"Bot {_token}");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                HttpLogger.RegisterSlashCommandsFailed(_logger, (int)response.StatusCode, await response.Content.ReadAsStringAsync(), null);
                return;
            }

            RegisterCommands(await response.Content.ReadFromJsonAsync<ApplicationCommand[]>(_jsonSerializerOptions) ?? throw new InvalidOperationException("Failed to parse slash command response."), localCommands);
            HttpLogger.RegisterSlashCommands(_logger, null);
        }

        private void RegisterCommands(ApplicationCommand[] receivedCommands, List<ImmutableCommand> localCommands)
        {
            Dictionary<string, ImmutableCommand> commands = new();
            foreach (ApplicationCommand applicationCommand in receivedCommands)
            {
                foreach (ImmutableCommand localCommand in localCommands)
                {
                    if (applicationCommand.Name != localCommand.Name)
                    {
                        continue;
                    }
                    else if (applicationCommand.Options.IsDefined() && applicationCommand.Options.Value.All(option => option.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup))
                    {
                        foreach (IApplicationCommandOption option in applicationCommand.Options.Value)
                        {
                            foreach (ImmutableCommand subcommand in localCommand.Subcommands)
                            {
                                if (subcommand.Name == option.Name)
                                {
                                    commands.Add($"{applicationCommand.Name} {subcommand.Name}", subcommand);
                                }
                            }
                        }
                    }
                    else
                    {
                        commands.Add(applicationCommand.Name, localCommand);
                    }
                }
            }

            Commands = commands.ToFrozenDictionary();
        }

        public InteractionResponse ExecuteCommand(Interaction interaction)
        {
            if (!Commands.TryGetValue(GetCommandName(interaction), out ImmutableCommand? command))
            {
                throw new ProviderException(ResponseStatus.NotFound, "Command not found.");
            }
            else if (command.IsGroupCommand)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Command is a group command.");
            }

            Task<InteractionResponse> task = command.ExecuteAsync(interaction, _serviceProvider!);
            return task.GetAwaiter().GetResult();
        }

        public InteractionAutocompleteCallbackData GetAutocompleteResult(Interaction interaction)
        {
            if (!Commands.TryGetValue(GetCommandName(interaction), out ImmutableCommand? command))
            {
                throw new ProviderException(ResponseStatus.NotFound, "Command not found.");
            }
            else if (command.IsGroupCommand)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Command is a group command.");
            }
            else if (!command.AutoCompleteProviders.TryGetValue(interaction.Data.Value.AsT0.Options.Value[0].Options.Value[0].Name, out IAutoCompleteProvider? provider) || provider is null)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Command Option does not have an autocomplete provider.");
            }
            else
            {
                Task<InteractionAutocompleteCallbackData> task = provider.GetAutocompleteResultAsync(interaction, interaction.Data.Value.AsT0.Options.Value[0]);
                return task.GetAwaiter().GetResult();
            }
        }

        private static string GetCommandName(Interaction interaction)
        {
            Span<char> span = stackalloc char[74];
            int position = interaction.Data.Value.AsT0.Name.Length;
            interaction.Data.Value.AsT0.Name.AsSpan().CopyTo(span);
            if (interaction.Data.Value.AsT0.Options.IsDefined())
            {
                IEnumerator<IApplicationCommandInteractionDataOption> options = interaction.Data.Value.AsT0.Options.Value.GetEnumerator();
                while (options.MoveNext())
                {
                    if (options.Current.Type is not ApplicationCommandOptionType.SubCommand and not ApplicationCommandOptionType.SubCommandGroup)
                    {
                        continue;
                    }

                    span[position++] = ' ';
                    options.Current.Name.AsSpan().CopyTo(span[position..(position + options.Current.Name.Length)]);
                    position += options.Current.Name.Length;
                    options = options.Current.Options.Value.GetEnumerator();
                }
            }

            return span[..position].ToString();
        }

        public void Dispose() => ((IDisposable)_httpClient).Dispose();
    }
}
