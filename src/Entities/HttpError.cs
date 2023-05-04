using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record HttpError([property: JsonPropertyName("error_message")] string ErrorMessage);
}
