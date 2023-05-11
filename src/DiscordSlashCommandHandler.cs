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
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker
{
    public sealed class DiscordSlashCommandHandler : IDisposable
    {
        public Snowflake CreateCookieCommandId { get; private set; }
        public FrozenDictionary<Snowflake, Command> Commands { get; private set; } = FrozenDictionary<Snowflake, Command>.Empty;

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
            List<Command> commands = new();
            foreach (Type type in typeof(DiscordSlashCommandHandler).Assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.GetCustomAttribute<CommandAttribute>() is not CommandAttribute commandAttribute)
                {
                    continue;
                }

                commands.Add(new Command(commandAttribute, type, serviceProvider));
            }

            HttpRequestMessage request = new(HttpMethod.Put, $"https://discord.com/api/v10/applications/{_applicationId}/commands")
            {
                Content = JsonContent.Create(commands.Select(command => (BulkApplicationCommandData)command).ToArray(), typeof(BulkApplicationCommandData[]), MediaTypeHeaderValue.Parse("application/json"), _jsonSerializerOptions)
            };
            request.Headers.Add("Authorization", $"Bot {_token}");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                HttpLogger.RegisterSlashCommandsFailed(_logger, (int)response.StatusCode, await response.Content.ReadAsStringAsync(), null);
                return;
            }

            ApplicationCommand[] receivedCommands = await response.Content.ReadFromJsonAsync<ApplicationCommand[]>(_jsonSerializerOptions) ?? throw new InvalidOperationException("Failed to parse slash command response.");

            // TODO: Application command handler
            CreateCookieCommandId = receivedCommands[0].ID;
            HttpLogger.RegisterSlashCommands(_logger, null);
        }

        public unsafe InteractionResponse ExecuteCommand(Interaction interaction)
        {
            if (Commands.TryGetValue(interaction.Data.Value.AsT0.ID, out Command? command))
            {
                Task<InteractionResponse> task = ((delegate*<Interaction, Task<InteractionResponse>>)command.MethodPointer)(interaction);
                return task.GetAwaiter().GetResult();
            }

            throw new ProviderException(ResponseStatus.NotFound, "Command not found.");
        }

        public void Dispose() => ((IDisposable)_httpClient).Dispose();
    }
}
