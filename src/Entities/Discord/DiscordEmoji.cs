using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord
{
    public sealed class DiscordEmoji
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        public DiscordEmoji(string? name = null) => Name = name;
    }
}
