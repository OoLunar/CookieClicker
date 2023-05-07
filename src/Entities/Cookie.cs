using System;
using System.ComponentModel.DataAnnotations;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record Cookie
    {
        [Key]
        public Ulid Id { get; init; } = Ulid.NewUlid();
        public decimal Clicks;
    }
}
