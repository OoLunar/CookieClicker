using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Components
{
    public sealed class DiscordButtonComponent : IDiscordComponent
    {
        [JsonPropertyName("type")]
        public DiscordComponentType Type { get; init; } = DiscordComponentType.Button;

        [JsonPropertyName("custom_id")]
        public string CustomId { get; init; } = "";

        [JsonPropertyName("label")]
        public string Label { get; init; } = "Click me!";

        [JsonPropertyName("style")]
        public DiscordButtonStyle Style { get; init; } = DiscordButtonStyle.Primary;

        [JsonPropertyName("emoji")]
        public DiscordEmoji Emoji { get; init; } = new("🍪");
    }
}
