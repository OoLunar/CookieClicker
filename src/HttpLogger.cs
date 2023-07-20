using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace OoLunar.CookieClicker
{
    public static class HttpLogger
    {
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, IPEndPoint, Exception?> ServerStart = LoggerMessage.Define<IPEndPoint>(LogLevel.Information, new EventId(0, "Server Setup"), "Server started on {ListeningAddress}");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Uri, int, string, Exception?> HttpHandleSuccess = LoggerMessage.Define<string, Uri, int, string>(LogLevel.Debug, new EventId(1, "Http Request Handled"), "Handled {Method} request to {Path} with status {Status} {Response}");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Uri, int, string, Exception?> HttpHandleBadClient = LoggerMessage.Define<string, Uri, int, string>(LogLevel.Information, new EventId(1, "Http Request Handled"), "Handled {Method} request to {Path} with status {Status} {Response}");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Uri, int, string, Exception?> HttpHandleInternalError = LoggerMessage.Define<string, Uri, int, string>(LogLevel.Error, new EventId(1, "Http Request Handled"), "Handled {Method} request to {Path} with status {Status} {Response}");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> RegisterSlashCommands = LoggerMessage.Define(LogLevel.Information, new EventId(2, "SlashCommands registered"), "Registered slash commands.");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, int, string, Exception?> RegisterSlashCommandsFailed = LoggerMessage.Define<int, string>(LogLevel.Error, new EventId(2, "SlashCommands failed to register"), "Failed to register slash commands: {HttpStatusCode} {ReasonPhrase}");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, int, Exception?> CookieCreated = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3, "Cookie Database Operation"), "Created {Count:N0} cookies!");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, int, Exception?> CookieUpdated = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3, "Cookie Database Operation"), "Updated {Count:N0} cookies!");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> BakingError = LoggerMessage.Define(LogLevel.Error, new EventId(3, "Cookie Database Operation"), "There was an error baking cookies!");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> DbConnectionSuccess = LoggerMessage.Define(LogLevel.Debug, new EventId(4, "Database"), "Connected to database.");
        internal static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> DbConnectionError = LoggerMessage.Define(LogLevel.Error, new EventId(4, "Database"), "Failed to connect to database.");

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
