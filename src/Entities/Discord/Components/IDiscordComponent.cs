using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities.Discord.Components
{
    public interface IDiscordComponent
    {
        [JsonPropertyName("type")]
        DiscordComponentType Type { get; init; }
    }
}
