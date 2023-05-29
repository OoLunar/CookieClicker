using System;
using System.Linq;
using System.Threading.Tasks;
using OoLunar.CookieClicker.Entities.CommandFramework;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Commands
{
    public sealed class ButtonAutoCompleteProvider : IAutoCompleteProvider
    {
        private CookieTracker _cookieTracker { get; }

        public ButtonAutoCompleteProvider(CookieTracker cookieTracker)
        {
            ArgumentNullException.ThrowIfNull(cookieTracker, nameof(cookieTracker));
            _cookieTracker = cookieTracker;
        }

        public Task<InteractionAutocompleteCallbackData> GetAutocompleteResultAsync(Interaction interaction, IApplicationCommandInteractionDataOption parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter, nameof(parameter));

            return Task.FromResult(new InteractionAutocompleteCallbackData(_cookieTracker
                .GetCookies(interaction.ChannelID.OrThrow(() => new InvalidOperationException("Missing channel id.")))
                .Select(cookie => new ApplicationCommandOptionChoice(cookie.Clicks.ToString("N0"), cookie.Id.ToString()))
                .ToList()));
        }
    }
}
