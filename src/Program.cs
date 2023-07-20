using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OoLunar.HyperSharp;
using Remora.Discord.API.Extensions;

namespace OoLunar.CookieClicker
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            ServiceCollection serviceCollection = new();
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
            configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
            configurationBuilder.AddEnvironmentVariables("CookieClicker_");
            configurationBuilder.AddCommandLine(args);

            IConfiguration configuration = configurationBuilder.Build();
            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddLogging(loggingBuilder => HttpLogger.ConfigureLogging(loggingBuilder, configuration));
            serviceCollection.AddOptions();
            serviceCollection.ConfigureDiscordJsonConverters("HyperSharp");
            serviceCollection.ConfigureHyperJsonConverters("HyperSharp");
            serviceCollection.AddSingleton<CookieTracker>();
            serviceCollection.AddSingleton<DiscordSlashCommandHandler>();
            serviceCollection.AddHyperSharp((serviceProvider, hyperConfiguration) =>
            {
                string? host = configuration.GetValue("server:address", "localhost")?.Trim();
                if (string.IsNullOrWhiteSpace(host))
                {
                    throw new InvalidOperationException("The listening address cannot be null or whitespace.");
                }

                if (!IPAddress.TryParse(host, out IPAddress? address))
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    address = addresses.Length != 0 ? addresses[0] : throw new InvalidOperationException("The listening address could not be resolved to an IP address.");
                }

                hyperConfiguration.ListeningEndpoint = new IPEndPoint(address, configuration.GetValue("listening:port", 8080));
                hyperConfiguration.AddResponders(typeof(Program).Assembly);
            });

            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            await serviceProvider.GetRequiredService<DiscordSlashCommandHandler>().RegisterAsync();
            serviceProvider.GetRequiredService<HyperServer>().Run();
            await Task.Delay(-1);
        }
    }
}
