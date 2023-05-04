using System;
using System.Collections.Generic;
using System.Globalization;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace OoLunar.CookieClicker
{
    public sealed class HttpLogger : IServerCompanion
    {
        private readonly ILogger<HttpLogger> _logger;

        public HttpLogger(ILogger<HttpLogger> logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        public void OnServerError(ServerErrorScope scope, Exception error) => _logger.LogError(error, "A {Scope} error has occured:", scope);
        public void OnRequestHandled(IRequest request, IResponse response)
        {
            if (response.Status.RawStatus is < 400)
            {
                _logger.LogDebug("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
            else if (response.Status.RawStatus is >= 500 and < 600)
            {
                _logger.LogError("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
            else
            {
                _logger.LogInformation("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
        }

        public static void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration)
        {
            string loggingFormat = configuration.GetValue("Logging:Format", "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}") ?? throw new InvalidOperationException("Logging:Format is null");
            string filename = configuration.GetValue("Logging:Filename", "yyyy'-'MM'-'dd' 'HH'.'mm'.'ss") ?? throw new InvalidOperationException("Logging:Filename is null");

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
                $"logs/{DateTime.Now.ToUniversalTime().ToString(filename, CultureInfo.InvariantCulture)}.log",
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
        }
    }
}
