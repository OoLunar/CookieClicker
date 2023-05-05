using System;
using System.Threading;

namespace OoLunar.CookieClicker.Entities
{
    public sealed record CachedCookie
    {
        public Cookie Cookie { get; init; }
        public DateTime LastModified { get; private set; }
        public bool IsSaved { get; internal set; }
        public bool Expired => DateTime.UtcNow - LastModified > TimeSpan.FromSeconds(5);

        public CachedCookie(Cookie cookie, bool saved)
        {
            Cookie = cookie;
            LastModified = DateTime.UtcNow;
            IsSaved = saved;
        }

        public ulong Click()
        {
            ulong value = Interlocked.Increment(ref Cookie.Clicks);
            LastModified = DateTime.UtcNow;
            return value;
        }
    }
}
