using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Interactions
{
    public sealed class DiscordInteractionResponse
    {
        [JsonPropertyName("type")]
        public DiscordInteractionResponseType Type { get; init; }

        [JsonPropertyName("data")]
        public DiscordInteractionCallbackData? Data { get; init; }
    }
}
