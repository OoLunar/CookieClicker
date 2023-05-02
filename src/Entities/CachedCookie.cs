using System;
using System.Threading;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record CachedCookie
    {
        public Cookie Cookie { get; init; }
        public DateTime LastModified { get; private set; }
        public bool IsSaved { get; init; }
        public bool CanSave => DateTime.UtcNow - LastModified > TimeSpan.FromSeconds(5);

        public CachedCookie(Cookie cookie, bool saved)
        {
            Cookie = cookie;
            LastModified = DateTime.UtcNow;
            IsSaved = saved;
        }

        public ulong Bake()
        {
            ulong value = Interlocked.Increment(ref Cookie._clicks);
            LastModified = DateTime.UtcNow;
            return value;
        }
    }
}
