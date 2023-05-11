using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion.Providers.Json;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker
{
    public sealed class RouteDispatcher
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly CookieTracker _cookieTracker;
        private readonly DiscordSlashCommandHandler _slashCommandHandler;

        public RouteDispatcher(CookieTracker cookieTracker, DiscordSlashCommandHandler slashCommandHandler, IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(cookieTracker, nameof(cookieTracker));
            ArgumentNullException.ThrowIfNull(slashCommandHandler, nameof(slashCommandHandler));
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

            _cookieTracker = cookieTracker;
            _slashCommandHandler = slashCommandHandler;
            _jsonSerializerOptions = jsonSerializerOptions.Get("Discord");
        }

        public IResponse Handle(IRequest request)
        {
            if (request.ContentType?.KnownType != ContentType.ApplicationJson)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Invalid content type");
            }
            else if (request.Content is null || request.Content.Length == 0)
            {
                throw new ProviderException(ResponseStatus.BadRequest, "Missing interaction data");
            }

            request.Content.Seek(0, SeekOrigin.Begin);
            Interaction interaction = JsonSerializer.Deserialize<Interaction>(request.Content, _jsonSerializerOptions) ?? throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction data");
            return request.Respond()
                .Status(ResponseStatus.OK)
                .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
                .Content(new JsonContent(interaction.Type switch
                {
                    InteractionType.Ping => new InteractionResponse(InteractionCallbackType.Pong),
                    InteractionType.ApplicationCommand => _slashCommandHandler.ExecuteCommand(interaction),
                    InteractionType.MessageComponent => ClickCookie(interaction),
                    _ => throw new ProviderException(ResponseStatus.BadRequest, $"Unknown interaction type: {interaction.Type}")
                }, _jsonSerializerOptions))
                .Build();
        }

        private InteractionResponse ClickCookie(Interaction interaction) => !Ulid.TryParse(interaction.Data.Value.AsT1.CustomID ?? throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction data"), out Ulid cookieId)
            ? throw new ProviderException(ResponseStatus.BadRequest, $"Invalid cookie id: {interaction.Data.Value.AsT1.CustomID}")
            : new InteractionResponse(InteractionCallbackType.UpdateMessage, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Content: $"The cookie has been clicked {_cookieTracker.Click(cookieId):N0} times!",
                Components: new List<IActionRowComponent>() {
                new ActionRowComponent(
                    Components: new List<IButtonComponent>() {
                        new ButtonComponent(
                            Emoji: new Optional<IPartialEmoji>(new Emoji(null, "🍪")),
                            CustomID: cookieId.ToString(),
                            Style: ButtonComponentStyle.Primary,
                            Label: "Click me!"
                        )
                    }
                )
            })));
    }
}
