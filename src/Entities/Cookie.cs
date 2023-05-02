using System;
using System.ComponentModel.DataAnnotations;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record Cookie
    {
        [Key]
        public Ulid Id { get; init; } = Ulid.NewUlid();
        public ulong Clicks => _clicks;

        internal ulong _clicks;
    }
}
