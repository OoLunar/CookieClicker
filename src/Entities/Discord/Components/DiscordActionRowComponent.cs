using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Components
{
    public sealed class DiscordActionRowComponent : IDiscordComponent
    {
        [JsonPropertyName("type")]
        public DiscordComponentType Type { get; init; } = DiscordComponentType.ActionRow;

        [JsonPropertyName("components")]
        public IReadOnlyList<object> Components { get; init; } = new List<object>();
    }
}
