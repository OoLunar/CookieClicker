using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Interactions
{
    public sealed class DiscordInteractionApplicationCommandDataStructure : IDiscordInteractionData
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = null!;
    }
}
