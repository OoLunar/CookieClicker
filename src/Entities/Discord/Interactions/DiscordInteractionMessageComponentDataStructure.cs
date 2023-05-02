using System.Text.Json.Serialization;
using OoLunar.CookieClicker.Entities.Discord.Components;

namespace OoLunar.CookieClicker.Entities.Discord.Interactions
{
    public sealed class DiscordInteractionMessageComponentDataStructure : IDiscordInteractionData
    {
        [JsonPropertyName("custom_id")]
        public string CustomId { get; init; } = "";

        [JsonPropertyName("component_type")]
        public DiscordComponentType ComponentType { get; init; }
    }
}
