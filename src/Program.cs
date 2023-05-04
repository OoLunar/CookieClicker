using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using GenHTTP.Api.Content.Templating;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Engine;
using GenHTTP.Modules.Authentication;
using GenHTTP.Modules.DirectoryBrowsing;
using GenHTTP.Modules.ErrorHandling;
using GenHTTP.Modules.Functional;
using GenHTTP.Modules.Functional.Provider;
using GenHTTP.Modules.IO;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Layouting.Provider;
using GenHTTP.Modules.Markdown;
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

                MarkdownPageProviderBuilder<IModel> readme = ModMarkdown
                        .Page(Resource.FromAssembly(typeof(Program).Assembly, "Readme.md"))
                        .Title("Readme - Cookie Clicker")
                        .Description("Are you tired of having no purpose in life? Do you crave the sweet satisfaction of watching a number increase every time you click? Look no further, because the cookie clicker bot is here to fill that void in your soul.");

                LayoutBuilder textFiles = Layout.Create()
                    .Add("readme", readme)
                    .Add("license", ModMarkdown.Page(Resource.FromAssembly(typeof(Program).Assembly, "License"))
                        .Title("License - Cookie Clicker")
                        .Description("The LGPL 3 is a permissive open-source license that allows the use and modification of software, while requiring any changes made to the original code to be released under the same LGPL 3 license. The license also allows for the linking of the licensed code with proprietary software under certain conditions."))
                    .Add("privacy", ModMarkdown.Page(Resource.FromAssembly(typeof(Program).Assembly, "PrivacyPolicy.md"))
                        .Title("Privacy - Cookie Clicker")
                        .Description("Privacy Policy for the Cookie Clicker Discord bot: Your data is only collected for the purpose of providing services and is securely stored. We won't share your data with third parties, except as required by law or legal process, or to protect the rights, property, or safety of the Bot, its users, or others."))
                    .Add("terms", ModMarkdown.Page(Resource.FromAssembly(typeof(Program).Assembly, "TermsOfService.md"))
                        .Title("Terms of Service - Cookie Clicker")
                        .Description("Cookie Clicker Discord bot's Terms of Service outline prohibited conduct, user responsibility, disclaimer of warranty and liability, indemnification, termination, and changes to the terms and conditions. By using the bot, you agree to these terms.")
                    )
                    .Add("favicon.ico", Download.From(Resource.FromAssembly(typeof(Program).Assembly, "res.favicon.ico")))
                    .Add("res", Listing.From(ResourceTree.FromAssembly(typeof(Program).Assembly, "res")))
                    .Index(readme);

                InlineBuilder inlineBuilder = Inline.Create()
                    .Authentication(ApiKeyAuthentication.Create()
                        .WithHeader("Host")
                        .Authenticator(discordHeaderAuthentication.Authenticate))
                    .Post(interactionHandler.Handle);

                return Host.Create()
                    .Defaults()
                    .Companion(serilogCompanion)
                    .Handler(Layout.Create()
                        .Add(textFiles)
                        .Add(configuration.GetValue("Server:BasePath", "api")!.TrimStart('/'), inlineBuilder)
                        .Add(ErrorHandler.From(jsonErrorMapper)))
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
