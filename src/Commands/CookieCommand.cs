using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Attributes;
using OoLunar.CookieClicker.Entities.CommandFramework;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker.Commands
{
    [Command("cookie", "Let's click some cookies!"), RequirePermission(DiscordPermission.ManageMessages)]
    public sealed record CookieCommand
    {
        private readonly ILogger<CookieCommand> _logger;
        private readonly CookieTracker _cookieTracker;

        public CookieCommand(ILogger<CookieCommand> logger, CookieTracker cookieTracker)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(cookieTracker, nameof(cookieTracker));

            _logger = logger;
            _cookieTracker = cookieTracker;
        }

        [Command("create", "Creates a cookie in the current channel.")]
        [CommandOption(ApplicationCommandOptionType.String, "message_text", "The text to display on the cookie.", false, 0)]
        [CommandOption(ApplicationCommandOptionType.String, "button_text", "The text to display on the button.", false, 1)]
        [CommandOption(ApplicationCommandOptionType.String, "button_emoji", "The emoji to display on the button.", false, 2)]
        public static Task<InteractionResponse> CreateAsync(Interaction interaction)
        {
            Entities.Cookie cookie = new();
            //_cookieTracker.CreateCookie(cookie);
            return Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
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

        [Command("update", "Updates the text on a cookie.")]
        [CommandOption<ButtonAutoCompleteProvider>(ApplicationCommandOptionType.String, "button_id", "The ID of the button to update.", true, 0)]
        [CommandOption(ApplicationCommandOptionType.String, "message_text", "The text to display on the cookie.", false, 1)]
        [CommandOption(ApplicationCommandOptionType.String, "button_text", "The text to display on the button.", false, 2)]
        [CommandOption(ApplicationCommandOptionType.String, "button_emoji", "The emoji to display on the button.", false, 3)]
        public static Task<InteractionResponse> UpdateAsync(Interaction interaction)
        {
            //_logger.LogError("Not implemented.");
            throw new NotImplementedException();
        }

        [Command("move", "Moves a cookie to the current channel.")]
        [CommandOption<ButtonAutoCompleteProvider>(ApplicationCommandOptionType.String, "button_id", "The ID of the button to move.", true)]
        public static Task<InteractionResponse> MoveAsync(Interaction interaction)
        {
            //_logger.LogError("Not implemented.");
            throw new NotImplementedException();
        }
    }
}
