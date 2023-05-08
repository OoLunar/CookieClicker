using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker
{
    public sealed class DiscordSlashCommandHandler : IDisposable
    {
        public Snowflake CreateCookieCommandId { get; private set; }

        private readonly string _token;
        private readonly string _applicationId;
        private readonly ILogger<DiscordSlashCommandHandler> _logger;
        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public DiscordSlashCommandHandler(IConfiguration configuration, ILogger<DiscordSlashCommandHandler> logger, IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

            _token = configuration["Discord:Token"] ?? throw new ArgumentException("Discord token is not specified.");
            _applicationId = configuration["Discord:ApplicationId"] ?? throw new ArgumentException("Discord application id is not specified.");
            _logger = logger;
            _jsonSerializerOptions = jsonSerializerOptions.Get("Discord");

            string userAgent = configuration.GetValue("Discord:UserAgent", "OoLunar.CookieClicker")!;
            string githubUrl = configuration.GetValue("Discord:GithubUrl", "https://github.com/OoLunar/CookieClicker")!;
            string version = typeof(DiscordSlashCommandHandler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{userAgent} ({githubUrl}, v{version})");
        }

        public async Task RegisterAsync()
        {
            HttpRequestMessage request = new(HttpMethod.Put, $"https://discord.com/api/v10/applications/{_applicationId}/commands") { Content = new StringContent("""[{"name":"create-cookie","description":"Creates a cookie in the current channel.","type":1,"default_member_permissions":"8192","dm_permission":true}]""", MediaTypeHeaderValue.Parse("application/json")) };
            request.Headers.Add("Authorization", $"Bot {_token}");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                HttpLogger.RegisterSlashCommandsFailed(_logger, (int)response.StatusCode, await response.Content.ReadAsStringAsync(), null);
                return;
            }

            ApplicationCommand[] commands = await response.Content.ReadFromJsonAsync<ApplicationCommand[]>(_jsonSerializerOptions) ?? throw new InvalidOperationException("Failed to parse slash command response.");

            // TODO: Application command handler
            CreateCookieCommandId = commands[0].ID;
            HttpLogger.RegisterSlashCommands(_logger, null);
        }

        public void Dispose() => ((IDisposable)_httpClient).Dispose();
    }
}
