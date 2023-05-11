using System.Threading.Tasks;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Entities.CommandFramework
{
    public interface IAutoCompleteProvider
    {
        public Task<InteractionAutocompleteCallbackData> GetAutocompleteResultAsync(ApplicationCommandOption parameter, string input);
    }
}
