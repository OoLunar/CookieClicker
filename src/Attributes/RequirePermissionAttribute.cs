using System;
using Remora.Discord.API.Abstractions.Objects;

namespace OoLunar.CookieClicker.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequirePermissionAttribute : Attribute
    {
        public DiscordPermission Permission { get; init; }
        public RequirePermissionAttribute(DiscordPermission permission) => Permission = permission;
    }
}
