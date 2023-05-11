using System;
using System.Threading.Tasks;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Entities.CommandFramework
{
    public sealed class ButtonAutoCompleteProvider : IAutoCompleteProvider
    {
        public Task<InteractionAutocompleteCallbackData> GetAutocompleteResultAsync(ApplicationCommandOption parameter, string input) => throw new NotImplementedException();
    }
}
