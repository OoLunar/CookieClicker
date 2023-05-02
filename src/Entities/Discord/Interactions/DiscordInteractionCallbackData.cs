using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Interactions
{
    public sealed class DiscordInteractionCallbackData
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = "The cookie has been clicked 0 times!";

        [JsonPropertyName("components")]
        public IReadOnlyList<object> Components { get; init; } = new List<object>();
    }
}
