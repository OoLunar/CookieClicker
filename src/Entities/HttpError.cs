using System.Text.Json.Serialization;

namespace OoLunar.CookieClicker.Entities
{
    public readonly record struct HttpError([property: JsonPropertyName("error_message")] string ErrorMessage);
}
