using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Attributes;
using OoLunar.CookieClicker.Entities;
using Remora.Discord.API;
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
        public Task<InteractionResponse> CreateAsync(Interaction interaction) => Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
            Content: $"Click the cookie to get started!",
            Components: new List<IActionRowComponent>() {
                new ActionRowComponent(
                    Components: new List<IButtonComponent>() {
                        new ButtonComponent(
                            Emoji: new Optional<IPartialEmoji>(new Emoji(null, "🍪")),
                            CustomID: _cookieTracker.CreateCookie(interaction.GuildID.OrDefault(), interaction.ChannelID.OrThrow(() => new InvalidOperationException("Missing channel id.")), default).Id.ToString(),
                            Style: ButtonComponentStyle.Primary,
                            Label: "Click me!"
                        )
                    }
                )
            }
        ))));

        [Command("update", "Updates the text on a cookie.")]
        [CommandOption<ButtonAutoCompleteProvider>(ApplicationCommandOptionType.String, "button_id", "The ID of the button to update.", true, 0)]
        [CommandOption(ApplicationCommandOptionType.String, "message_text", "The text to display on the cookie.", false, 1)]
        [CommandOption(ApplicationCommandOptionType.String, "button_text", "The text to display on the button.", false, 2)]
        [CommandOption(ApplicationCommandOptionType.String, "button_emoji", "The emoji to display on the button.", false, 3)]
        public Task<InteractionResponse> UpdateAsync(Interaction interaction)
        {
            _logger.LogError("Not implemented.");
            throw new NotImplementedException();
        }

        [Command("move", "Moves a cookie to the current channel.")]
        [CommandOption<ButtonAutoCompleteProvider>(ApplicationCommandOptionType.String, "button_id", "The ID of the button to move.", true)]
        public Task<InteractionResponse> MoveAsync(Interaction interaction)
        {
            if (!Ulid.TryParse(interaction.Data.Value.AsT0.Options.Value[0].Value.Value.AsT0, out Ulid cookieId))
            {
                return Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                    Content: $"Invalid cookie id: {interaction.Data.Value.AsT0.Options.Value[0].Value.Value.AsT0}",
                    Flags: MessageFlags.Ephemeral
                ))));
            }

            // TODO: Delete the old cookie message.
            Cookie cookie = _cookieTracker.GetCookie(cookieId);
            cookie.ChannelId = interaction.ChannelID.OrThrow(() => new InvalidDataException("Missing channel id."));
            cookie.MessageId = default;
            bool hasUpdated = _cookieTracker.UpdateCookie(cookieId, cookie);
            return Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Content: $"The cookie has been clicked {cookie.Clicks:N0} times!",
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

        [Command("stats", "General statistics about a cookie.")]
        [CommandOption<ButtonAutoCompleteProvider>(ApplicationCommandOptionType.String, "button_id", "The ID of the button to get stats for.", true)]
        public Task<InteractionResponse> StatsAsync(Interaction interaction)
        {
            if (!Ulid.TryParse(interaction.Data.Value.AsT0.Options.Value[0].Value.Value.AsT0, out Ulid cookieId))
            {
                return Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                    Content: $"Invalid cookie id: {interaction.Data.Value.AsT0.Options.Value[0].Value.Value.AsT0}",
                    Flags: MessageFlags.Ephemeral
                ))));
            }

            Cookie cookie = _cookieTracker.GetCookie(cookieId);
            return Task.FromResult(new InteractionResponse(InteractionCallbackType.ChannelMessageWithSource, new Optional<OneOf.OneOf<IInteractionMessageCallbackData, IInteractionAutocompleteCallbackData, IInteractionModalCallbackData>>(new InteractionMessageCallbackData(
                Embeds: new List<IEmbed>() {
                    new Embed(
                        Title: $"Cookie Stats",
                        Description: $"The cookie has been clicked {cookie.Clicks:N0} times!",
                        Fields: new List<IEmbedField>() {
                            new EmbedField(
                                Name: "Id",
                                Value: $"{cookie.Id}",
                                IsInline: true
                            ),
                            new EmbedField(
                                Name: "Magic Numbers",
                                Value: $"{Unsafe.As<byte, ushort>(ref cookie.Id.Random[0]):N0} {Unsafe.As<byte, ushort>(ref cookie.Id.Random[2]):N0} {Unsafe.As<byte, ushort>(ref cookie.Id.Random[4]):N0} {Unsafe.As<byte, ushort>(ref cookie.Id.Random[6]):N0} {Unsafe.As<byte, ushort>(ref cookie.Id.Random[8]):N0}",
                                IsInline: true
                            ),
                            new EmbedField(
                                Name: "Created At",
                                Value: $"<t:{Snowflake.CreateTimestampSnowflake(cookie.Id.Time, Constants.DiscordEpoch)}:R>",
                                IsInline: true
                            )
                        }
                    )
                }
            ))));
        }
    }
}
