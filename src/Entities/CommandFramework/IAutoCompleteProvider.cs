using System.Threading.Tasks;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Entities.CommandFramework
{
    public interface IAutoCompleteProvider
    {
        public Task<InteractionAutocompleteCallbackData> GetAutocompleteResultAsync(Interaction interaction, IApplicationCommandInteractionDataOption parameter);
    }
}
