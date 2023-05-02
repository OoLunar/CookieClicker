using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Engine;
using GenHTTP.Modules.Authentication;
using GenHTTP.Modules.ErrorHandling;
using GenHTTP.Modules.Functional;
using GenHTTP.Modules.Practices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Database;
using OoLunar.CookieClicker.GenHttp;
using OoLunar.CookieClicker.Headers;
using OoLunar.CookieClicker.Routes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace OoLunar.CookieClicker
{
    public sealed class Program
    {
        public static async Task<int> Main(string[] args)
        {
            ServiceCollection serviceCollection = new();
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.AddJsonFile("config.json", true, true);
            configurationBuilder.AddEnvironmentVariables("CookieClicker_");
            configurationBuilder.AddCommandLine(args);

            IConfiguration configuration = configurationBuilder.Build();
            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddLogging(loggingBuilder =>
            {
                const string loggingFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

                // Log both to console and the file
                LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Is(configuration.GetValue("Logging:Level", LogEventLevel.Debug))
                .WriteTo.Console(outputTemplate: loggingFormat, theme: new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
                {
                    [ConsoleThemeStyle.Text] = "\x1b[0m",
                    [ConsoleThemeStyle.SecondaryText] = "\x1b[90m",
                    [ConsoleThemeStyle.TertiaryText] = "\x1b[90m",
                    [ConsoleThemeStyle.Invalid] = "\x1b[31m",
                    [ConsoleThemeStyle.Null] = "\x1b[95m",
                    [ConsoleThemeStyle.Name] = "\x1b[93m",
                    [ConsoleThemeStyle.String] = "\x1b[96m",
                    [ConsoleThemeStyle.Number] = "\x1b[95m",
                    [ConsoleThemeStyle.Boolean] = "\x1b[95m",
                    [ConsoleThemeStyle.Scalar] = "\x1b[95m",
                    [ConsoleThemeStyle.LevelVerbose] = "\x1b[34m",
                    [ConsoleThemeStyle.LevelDebug] = "\x1b[90m",
                    [ConsoleThemeStyle.LevelInformation] = "\x1b[36m",
                    [ConsoleThemeStyle.LevelWarning] = "\x1b[33m",
                    [ConsoleThemeStyle.LevelError] = "\x1b[31m",
                    [ConsoleThemeStyle.LevelFatal] = "\x1b[97;91m"
                }))
                .WriteTo.File(
                    $"logs/{DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH'.'mm'.'ss", CultureInfo.InvariantCulture)}.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: loggingFormat
                );

                // Allow specific namespace log level overrides, which allows us to hush output from things like the database basic SELECT queries on the Information level.
                foreach (IConfigurationSection logOverride in configuration.GetSection("logging:overrides").GetChildren())
                {
                    if (logOverride.Value is null || !Enum.TryParse(logOverride.Value, out LogEventLevel logEventLevel))
                    {
                        continue;
                    }

                    loggerConfiguration.MinimumLevel.Override(logOverride.Key, logEventLevel);
                }

                loggingBuilder.AddSerilog(loggerConfiguration.CreateLogger());
            });

            serviceCollection.AddDbContext<CookieDatabaseContext>((services, options) => CookieDatabaseContext.ConfigureOptions(options, services.GetRequiredService<IConfiguration>()), ServiceLifetime.Scoped);
            serviceCollection.AddSingleton<CookieTracker>();
            serviceCollection.AddSingleton<DiscordHeaderAuthentication>();
            serviceCollection.AddSingleton<DiscordSlashCommandHandler>();
            serviceCollection.AddSingleton<InteractionHandler>();
            serviceCollection.AddSingleton<JsonErrorMapper>();
            serviceCollection.AddSingleton<SerilogCompanion>();
            serviceCollection.AddSingleton((serviceProvider) =>
            {
                DiscordHeaderAuthentication discordHeaderAuthentication = serviceProvider.GetRequiredService<DiscordHeaderAuthentication>();
                IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
                InteractionHandler interactionHandler = serviceProvider.GetRequiredService<InteractionHandler>();
                JsonErrorMapper jsonErrorMapper = serviceProvider.GetRequiredService<JsonErrorMapper>();
                SerilogCompanion serilogCompanion = serviceProvider.GetRequiredService<SerilogCompanion>();

                return Host.Create()
                    .Defaults()
                    .Companion(serilogCompanion)
                    .Handler(Inline.Create()
                        .Add(ErrorHandler.From(jsonErrorMapper))
                        .Authentication(ApiKeyAuthentication.Create()
                            .WithHeader("X-Signature-Ed25519")
                            .Authenticator(discordHeaderAuthentication.Authenticate))
                        .Post(configuration.GetValue("Server:BasePath", "/")!, interactionHandler.HandleAsync))
                    .Bind(IPAddress.Parse(configuration.GetValue("Server:Address", "127.0.0.1")!), configuration.GetValue<ushort>("Server:Port", 8080))
                    .RequestReadTimeout(TimeSpan.FromSeconds(configuration.GetValue("Server:RequestReadTimeout", 30)))
                    .RequestMemoryLimit(configuration.GetValue<uint>("Server:RequestMemoryLimit", 1024 * 1024 * 10));
            });

            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            await serviceProvider.GetRequiredService<DiscordSlashCommandHandler>().RegisterAsync();
            serviceProvider.GetRequiredService<ILogger<Program>>().LogInformation("Server started.");
            return serviceProvider.GetRequiredService<IServerHost>().Run();
        }
    }
}
