using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OoLunar.HyperSharp;
using OoLunar.HyperSharp.Responders;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker.Responders
{
    [DependsOn<DiscordHeaderVerifier>()]
    public sealed class DiscordInteractionRoute : IResponder
    {
        public string[] Implements { get; init; } = Array.Empty<string>();

        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly CookieTracker _cookieTracker;
        private readonly DiscordSlashCommandHandler _slashCommandHandler;
        private readonly ILogger<DiscordInteractionRoute> _logger;

        public DiscordInteractionRoute(CookieTracker cookieTracker, DiscordSlashCommandHandler slashCommandHandler, IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions, ILogger<DiscordInteractionRoute> logger)
        {
            ArgumentNullException.ThrowIfNull(cookieTracker, nameof(cookieTracker));
            ArgumentNullException.ThrowIfNull(slashCommandHandler, nameof(slashCommandHandler));
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions, nameof(jsonSerializerOptions));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _cookieTracker = cookieTracker;
            _slashCommandHandler = slashCommandHandler;
            _jsonSerializerOptions = jsonSerializerOptions.Get("HyperSharp");
            _logger = logger;
        }

        public async Task<Result<HyperStatus>> RespondAsync(HyperContext context)
        {
            if (!context.Headers.TryGetValue("content-type", out IReadOnlyList<string>? contentType) || contentType.Count != 1 || contentType[0] != "application/json")
            {
                return Result.Ok(new HyperStatus(HttpStatusCode.BadRequest, new(), "Invalid content type"));
            }

            Interaction? interaction = JsonSerializer.Deserialize<Interaction>(context.Metadata["Body"], _jsonSerializerOptions);
            if (interaction is null)
            {
                return Result.Ok(new HyperStatus(HttpStatusCode.BadRequest, new(), "Invalid interaction data"));
            }

            Result<InteractionResponse> response = interaction.Type switch
            {
                InteractionType.Ping => Result.Ok(new InteractionResponse(InteractionCallbackType.Pong)),
                InteractionType.ApplicationCommand => CreateCookie(interaction),
                InteractionType.MessageComponent => ClickCookie(interaction),
                _ => Result.Fail($"Unknown interaction type: {interaction.Type}"),
            };

            _logger.LogInformation("Responding to interaction {InteractionId} with {Response}", interaction.ID, response.Value);
            await context.RespondAsync(response.IsFailed
                ? new HyperStatus(HttpStatusCode.BadRequest, response.Errors)
                : new HyperStatus(HttpStatusCode.OK, response.Value),
            _jsonSerializerOptions);

            return Result.Ok(new HyperStatus(HttpStatusCode.OK));
        }

        private Result<InteractionResponse> CreateCookie(Interaction interaction)
        {
            if (interaction.Data.Value.AsT0.ID != _slashCommandHandler.CreateCookieCommandId)
            {
                return Result.Fail($"Unknown command id: {interaction.Data.Value.AsT0.ID}");
            }

            Entities.Cookie cookie = new();
            _cookieTracker.CreateCookie(cookie);
            return Result.Ok(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Content: $"Click the cookie to get started!",
                Components: new List<IActionRowComponent>() {
                    new ActionRowComponent(
                        Components: new List<IButtonComponent>() {
                            new ButtonComponent(
                                Emoji: new Optional<IPartialEmoji>(new Emoji(null, "🍪")),
                                CustomID: cookie.Id.ToString(),
                                Style: ButtonComponentStyle.Primary,
                                Label: "Click me!"
                            )
                        }
                    )
                }
            ))));
        }

        private Result<InteractionResponse> ClickCookie(Interaction interaction)
        {
            if (string.IsNullOrWhiteSpace(interaction.Data.Value.AsT1.CustomID))
            {
                // throw new ProviderException(ResponseStatus.BadRequest, "Invalid interaction data");
                return Result.Fail("Invalid interaction data");
            }
            else if (!Ulid.TryParse(interaction.Data.Value.AsT1.CustomID, out Ulid cookieId))
            {
                // throw new ProviderException(ResponseStatus.BadRequest, $"Invalid cookie id: {interaction.Data.Value.AsT1.CustomID}");
                return Result.Fail($"Invalid cookie id: {interaction.Data.Value.AsT1.CustomID}");
            }
            else
            {
                return Result.Ok(new InteractionResponse(InteractionCallbackType.UpdateMessage, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                    Content: $"The cookie has been clicked {_cookieTracker.Click(cookieId):N0} times!",
                    Components: new List<IActionRowComponent>()
                    {
                        new ActionRowComponent(
                            Components: new List<IButtonComponent>()
                            {
                                new ButtonComponent(
                                    Emoji: new Optional<IPartialEmoji>(new Emoji(null, "🍪")),
                                    CustomID: cookieId.ToString(),
                                    Style: ButtonComponentStyle.Primary,
                                    Label: "Click me!"
                                )
                            }
                        )
                    }
                ))));
            }
        }
    }
}
