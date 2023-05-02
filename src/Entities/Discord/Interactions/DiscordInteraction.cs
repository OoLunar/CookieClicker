using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Interactions
{
    public sealed class DiscordInteraction
    {
        [JsonPropertyName("type")]
        public DiscordInteractionType Type { get; init; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; init; }
    }
}
