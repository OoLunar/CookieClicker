using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Database;
using OoLunar.CookieClicker.Entities;

namespace OoLunar.CookieClicker
{
    public sealed class CookieTracker : IAsyncDisposable
    {
        private readonly Dictionary<Ulid, CachedCookie> UnbakedCookies = new();
        private readonly CookieDatabaseContext DatabaseContext;
        private readonly ILogger<CookieTracker> Logger;
        private readonly SemaphoreSlim Semaphore = new(1, 1);
        private readonly PeriodicTimer Timer;
        private readonly Task BakingTask;

        public CookieTracker(CookieDatabaseContext databaseContext, IConfiguration configuration, ILogger<CookieTracker> logger)
        {
            ArgumentNullException.ThrowIfNull(databaseContext, nameof(databaseContext));
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            DatabaseContext = databaseContext;
            Logger = logger;
            Timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.GetValue("CookieTracker:Period", 30)));
            BakingTask = StartBakingAsync();
        }

        public void CreateCookie(Cookie cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie, nameof(cookie));
            UnbakedCookies.Add(cookie.Id, new(cookie, false));
        }

        public async Task<ulong> ClickAsync(Ulid cookieId)
        {
            // Check if the cookie is in the cache. If it isn't, pull it from the database.
            if (!UnbakedCookies.TryGetValue(cookieId, out CachedCookie? unbakedCookie))
            {
                await Semaphore.WaitAsync();
                try
                {
                    Cookie? cookie = DatabaseContext.Cookies.FirstOrDefault(x => x.Id == cookieId);
                    if (cookie is null)
                    {
                        throw new ArgumentException($"No cookie with id {cookieId} exists in the database.", nameof(cookieId));
                    }

                    unbakedCookie = new(cookie, true);
                    UnbakedCookies.Add(cookie.Id, unbakedCookie);
                }
                finally
                {
                    Semaphore.Release();
                }
            }

            // This method atomically increments the cookie's click count and returns the new value, while also resetting the cookie's timeout.
            return unbakedCookie.Bake();
        }

        private async Task StartBakingAsync()
        {
            while (await Timer.WaitForNextTickAsync())
            {
                await Semaphore.WaitAsync();
                try
                {
                    // Iterate over a copy of the unbaked cookies dictionary to avoid collection modified exceptions.
                    // This foreach loop is used to update the cookies in the database and remove them from cache.
                    foreach (CachedCookie cookie in UnbakedCookies.Values.ToArray())
                    {
                        // Check if the cookie can be saved.
                        // By default there's a 5 second timeout after the cookie has been clicked before it's written to the database.
                        if (!cookie.CanSave)
                        {
                            continue;
                        }

                        // Remove the cookie from the unbaked cookies dictionary. Prefer the returned result over the current result.
                        if (!UnbakedCookies.Remove(cookie.Cookie.Id, out CachedCookie? unbakedCookie))
                        {
                            unbakedCookie = cookie;
                        }

                        // Check if the cookie has been saved before.
                        if (unbakedCookie.IsSaved)
                        {
                            DatabaseContext.Cookies.Update(unbakedCookie.Cookie);
                        }
                        else
                        {
                            await DatabaseContext.Cookies.AddAsync(unbakedCookie.Cookie);
                        }
                    }

                    int count = await DatabaseContext.SaveChangesAsync();
                    if (count != 0)
                    {
                        Logger.LogDebug("Saved {ItemCount:N0} cookies!", count);
                    }
                }
                finally
                {
                    Semaphore.Release();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            Timer.Dispose();
            await BakingTask;
            await DatabaseContext.DisposeAsync();
            Semaphore.Dispose();
        }
    }
}
