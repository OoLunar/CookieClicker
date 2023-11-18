using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using OoLunar.CookieClicker.Entities.CommandFramework;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Commands
{
    public sealed class ButtonAutoCompleteProvider(CookieTracker cookieTracker) : IAutoCompleteProvider
    {
        private readonly CookieTracker _cookieTracker = cookieTracker ?? throw new ArgumentNullException(nameof(cookieTracker));

        public Task<InteractionAutocompleteCallbackData> GetAutocompleteResultAsync(Interaction interaction, IApplicationCommandInteractionDataOption parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter, nameof(parameter));

            return Task.FromResult(new InteractionAutocompleteCallbackData(_cookieTracker
                .GetCookies(interaction.ChannelID.OrThrow(() => new InvalidOperationException("Missing channel id.")))
                .Select(cookie => new ApplicationCommandOptionChoice(cookie.Clicks.ToString("N0", CultureInfo.InvariantCulture), cookie.Id.ToString()))
                .ToList()));
        }
    }
}
