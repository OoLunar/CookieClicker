using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using OoLunar.CookieClicker.Entities.Discord.Components;
using OoLunar.CookieClicker.Entities.Discord.Interactions;

namespace OoLunar.CookieClicker.Routes
{
    public sealed class InteractionHandler
    {
        private readonly CookieTracker _cookieTracker;
        private readonly DiscordSlashCommandHandler _slashCommandHandler;

        public InteractionHandler(CookieTracker cookieTracker, DiscordSlashCommandHandler slashCommandHandler)
        {
            ArgumentNullException.ThrowIfNull(cookieTracker, nameof(cookieTracker));
            ArgumentNullException.ThrowIfNull(slashCommandHandler, nameof(slashCommandHandler));
            _cookieTracker = cookieTracker;
            _slashCommandHandler = slashCommandHandler;
        }

        public async Task<DiscordInteractionResponse?> HandleAsync(IRequest request)
        {
            if (request.Content is null)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Missing interaction");
            }

            request.Content.Seek(0, SeekOrigin.Begin);
            DiscordInteraction interaction = await JsonSerializer.DeserializeAsync<DiscordInteraction>(request.Content) ?? throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction");
            return interaction.Type switch
            {
                DiscordInteractionType.Ping => new DiscordInteractionResponse() { Type = DiscordInteractionResponseType.Pong },
                DiscordInteractionType.ApplicationCommand => await CreateCookieAsync(interaction),
                DiscordInteractionType.MessageComponent => await ClickCookieAsync(interaction),
                _ => throw new ProviderException(ResponseStatus.BadRequest, $"Unknown interaction type: {interaction.Type}")
            };
        }

        private Task<DiscordInteractionResponse?> CreateCookieAsync(DiscordInteraction interaction)
        {
            DiscordInteractionApplicationCommandDataStructure? data = interaction.Data.Deserialize<DiscordInteractionApplicationCommandDataStructure>();
            if (data is null)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction data");
            }
            else if (data.Id != _slashCommandHandler.CreateCookieCommandId)
            {
                throw new ProviderException(ResponseStatus.BadRequest, $"Unknown command id: {data.Id}");
            }

            Entities.Cookie cookie = new();
            _cookieTracker.CreateCookie(cookie);
            return Task.FromResult<DiscordInteractionResponse?>(new DiscordInteractionResponse()
            {
                Type = DiscordInteractionResponseType.ChannelMessageWithSource,
                Data = new DiscordInteractionCallbackData()
                {
                    Components = new List<DiscordActionRowComponent>() {
                        new DiscordActionRowComponent()
                        {
                            Components = new List<DiscordButtonComponent>()
                            {
                                new DiscordButtonComponent()
                                {
                                    CustomId = cookie.Id.ToString()
                                }
                            }
                        }
                    }
                }
            });
        }

        private async Task<DiscordInteractionResponse?> ClickCookieAsync(DiscordInteraction interaction)
        {
            DiscordInteractionMessageComponentDataStructure data = interaction.Data.Deserialize<DiscordInteractionMessageComponentDataStructure>()!;
            if (!Ulid.TryParse(data.CustomId, out Ulid cookieId))
            {
                throw new ProviderException(ResponseStatus.BadRequest, $"Invalid cookie id: {data.CustomId}");
            }

            ulong count = await _cookieTracker.ClickAsync(cookieId);
            return new DiscordInteractionResponse()
            {
                Type = DiscordInteractionResponseType.UpdateMessage,
                Data = new DiscordInteractionCallbackData()
                {
                    Content = $"The cookie has been clicked {count:N0} times!",
                    Components = new List<DiscordActionRowComponent>() {
                        new DiscordActionRowComponent()
                        {
                            Components = new List<DiscordButtonComponent>()
                            {
                                new DiscordButtonComponent()
                                {
                                    CustomId = cookieId.ToString()
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
