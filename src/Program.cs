using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
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
using OoLunar.CookieClicker.GenHttp;
using OoLunar.CookieClicker.Headers;
using Remora.Discord.API.Extensions;

namespace OoLunar.CookieClicker
{
    public sealed class Program
    {
        public static async Task<int> Main(string[] args)
        {
            ServiceCollection serviceCollection = new();
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddJsonFile("config.json", true, true);
            configurationBuilder.AddEnvironmentVariables("CookieClicker_");
            configurationBuilder.AddCommandLine(args);

            IConfiguration configuration = configurationBuilder.Build();
            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddLogging(loggingBuilder => HttpLogger.ConfigureLogging(loggingBuilder, configuration));
            serviceCollection.AddOptions();
            serviceCollection.ConfigureDiscordJsonConverters();
            serviceCollection.AddSingleton(serviceProvider =>
            {
                HttpClient httpClient = new();
                string userAgent = configuration.GetValue("Discord:UserAgent", "OoLunar.CookieClicker")!;
                string githubUrl = configuration.GetValue("Discord:GithubUrl", "https://github.com/OoLunar/CookieClicker")!;
                string version = typeof(DiscordSlashCommandHandler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
                httpClient.DefaultRequestHeaders.UserAgent.Clear();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{userAgent} ({githubUrl}, v{version})");
                return httpClient;
            });
            serviceCollection.AddSingleton<CookieTracker>();
            serviceCollection.AddSingleton<DiscordHeaderAuthentication>();
            serviceCollection.AddSingleton<DiscordSlashCommandHandler>();
            serviceCollection.AddSingleton<RouteDispatcher>();
            serviceCollection.AddSingleton<JsonErrorMapper>();
            serviceCollection.AddSingleton<HttpLogger>();
            serviceCollection.AddSingleton((serviceProvider) =>
            {
                DiscordHeaderAuthentication discordHeaderAuthentication = serviceProvider.GetRequiredService<DiscordHeaderAuthentication>();
                IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
                RouteDispatcher interactionHandler = serviceProvider.GetRequiredService<RouteDispatcher>();
                JsonErrorMapper jsonErrorMapper = serviceProvider.GetRequiredService<JsonErrorMapper>();
                HttpLogger httpLogger = serviceProvider.GetRequiredService<HttpLogger>();

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
                        .Description("Cookie Clicker Discord bot's Terms of Service outline prohibited conduct, user responsibility, disclaimer of warranty and liability, indemnification, termination, and changes to the terms and conditions. By using the bot, you agree to these terms."))
                    .Add("favicon.ico", Download.From(Resource.FromAssembly(typeof(Program).Assembly, "favicon.ico")))
                    .Add("res", Listing.From(ResourceTree.FromAssembly(typeof(Program).Assembly, "res")))
                    .Index(readme);

                InlineBuilder inlineBuilder = Inline.Create()
                    .Authentication(ApiKeyAuthentication.Create()
                        .WithHeader("Host")
                        .Authenticator(discordHeaderAuthentication.Authenticate))
                    .Post(interactionHandler.Handle);

                LayoutBuilder root = Layout.Create()
                    .Add(textFiles)
                    .Add("api", inlineBuilder);

                string? basePath = configuration.GetValue<string>("Server:BasePath")?.TrimStart('/');
                return Host.Create()
                    .Defaults()
                    .Companion(httpLogger)
                    .Handler((string.IsNullOrWhiteSpace(basePath)
                        ? Layout.Create().Add(root)
                        : Layout.Create().Add(basePath, root))
                            .Add(ErrorHandler.From(jsonErrorMapper)))
                    .Bind(IPAddress.Parse(configuration.GetValue("Server:Address", "127.0.0.1")!), configuration.GetValue<ushort>("Server:Port", 8080))
                    .RequestReadTimeout(TimeSpan.FromSeconds(configuration.GetValue("Server:RequestReadTimeout", 30)))
                    .RequestMemoryLimit(configuration.GetValue<uint>("Server:RequestMemoryLimit", 1024 * 1024 * 10));
            });

            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            await serviceProvider.GetRequiredService<DiscordSlashCommandHandler>().RegisterAsync(serviceProvider);
            HttpLogger.ServerStart(serviceProvider.GetRequiredService<ILogger<Program>>(), configuration.GetValue("Server:Address", "127.0.0.1")!, configuration.GetValue<ushort>("Server:Port", 8080), null);
            return serviceProvider.GetRequiredService<IServerHost>().Run();
        }
    }
}
