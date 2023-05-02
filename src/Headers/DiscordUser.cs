using GenHTTP.Api.Content.Authentication;

namespace OoLunar.CookieClicker.Headers
{
    public sealed class DiscordUser : IUser
    {
        public string DisplayName { get; init; } = "Discord";
    }
}
