using System;
using System.Collections.Generic;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

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

        public InteractionResponse? Handle(Interaction interaction) => interaction.Type switch
        {
            InteractionType.Ping => new InteractionResponse(InteractionCallbackType.Pong),
            InteractionType.ApplicationCommand => CreateCookie(interaction),
            InteractionType.MessageComponent => ClickCookie(interaction),
            _ => throw new ProviderException(ResponseStatus.BadRequest, $"Unknown interaction type: {interaction.Type}")
        };

        private InteractionResponse? CreateCookie(Interaction interaction)
        {
            if (interaction.Data.Value.AsT0.ID != _slashCommandHandler.CreateCookieCommandId)
            {
                throw new ProviderException(ResponseStatus.BadRequest, $"Unknown command id: {interaction.Data.Value.AsT0.ID}");
            }

            Entities.Cookie cookie = new();
            _cookieTracker.CreateCookie(cookie);
            return new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Content: $"Click the cookie to get started!",
                Components: new List<IActionRowComponent>() {
                    new ActionRowComponent(
                        Components: new List<IButtonComponent>() {
                            new ButtonComponent(
                                CustomID: cookie.Id.ToString(),
                                Style: ButtonComponentStyle.Primary,
                                Label: "Click the cookie!"
                            )
                        }
                    )
                }
            )));
        }

        private InteractionResponse? ClickCookie(Interaction interaction) => !Ulid.TryParse(interaction.Data.Value.AsT1.CustomID ?? throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction data"), out Ulid cookieId)
            ? throw new ProviderException(ResponseStatus.BadRequest, $"Invalid cookie id: {interaction.Data.Value.AsT1.CustomID}")
            : new InteractionResponse(InteractionCallbackType.UpdateMessage, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Content: $"The cookie has been clicked {_cookieTracker.Click(cookieId):N0} times!",
                Components: new List<IActionRowComponent>() {
                new ActionRowComponent(
                    Components: new List<IButtonComponent>() {
                        new ButtonComponent(
                            CustomID: cookieId.ToString(),
                            Style: ButtonComponentStyle.Primary,
                            Label: "Click the cookie!"
                        )
                    }
                )
            })));
    }
}
