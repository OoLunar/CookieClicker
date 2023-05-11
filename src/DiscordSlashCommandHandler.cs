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
        public FrozenDictionary<string, Command> Commands { get; private set; } = FrozenDictionary<string, Command>.Empty;

        private readonly string _token;
        private readonly Snowflake _applicationId;
        private readonly ILogger<DiscordSlashCommandHandler> _logger;
        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        [SuppressMessage("Roslyn", "IDE0045", Justification = "Ternary operator rabbit hole.")]
        public DiscordSlashCommandHandler(IConfiguration configuration, ILogger<DiscordSlashCommandHandler> logger, IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

            _token = configuration["Discord:Token"] ?? throw new ArgumentException("Discord token is not specified.");
            _logger = logger;
            _jsonSerializerOptions = jsonSerializerOptions.Get("Discord");
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

            string userAgent = configuration.GetValue("Discord:UserAgent", "OoLunar.CookieClicker")!;
            string githubUrl = configuration.GetValue("Discord:GithubUrl", "https://github.com/OoLunar/CookieClicker")!;
            string version = typeof(DiscordSlashCommandHandler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{userAgent} ({githubUrl}, v{version})");
        }

        public async Task RegisterAsync(IServiceProvider serviceProvider)
        {
            List<Command> localCommands = new();
            foreach (Type type in typeof(DiscordSlashCommandHandler).Assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.GetCustomAttribute<CommandAttribute>() is not CommandAttribute commandAttribute)
                {
                    continue;
                }

                localCommands.Add(new Command(commandAttribute, type, serviceProvider));
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

        private void RegisterCommands(ApplicationCommand[] receivedCommands, List<Command> localCommands)
        {
            Dictionary<string, Command> commands = new();
            foreach (ApplicationCommand applicationCommand in receivedCommands)
            {
                foreach (Command localCommand in localCommands)
                {
                    if (applicationCommand.Name != localCommand.Name)
                    {
                        continue;
                    }
                    else if (applicationCommand.Options.IsDefined() && applicationCommand.Options.Value.All(option => option.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup))
                    {
                        foreach (IApplicationCommandOption option in applicationCommand.Options.Value)
                        {
                            foreach (Command subcommand in localCommand.Subcommands)
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

        public unsafe InteractionResponse ExecuteCommand(Interaction interaction)
        {
            if (Commands.TryGetValue(GetCommandName(interaction), out Command? command))
            {
                Task<InteractionResponse> task = command.MethodPointer!(interaction);
                return task.GetAwaiter().GetResult();
            }

            throw new ProviderException(ResponseStatus.NotFound, "Command not found.");
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
