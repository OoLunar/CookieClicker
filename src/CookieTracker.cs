using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
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
        private readonly FrozenDictionary<DatabaseOperation, NpgsqlCommand> DatabaseCommands;

        public CookieTracker(CookieDatabaseContext databaseContext, IConfiguration configuration, ILogger<CookieTracker> logger)
        {
            ArgumentNullException.ThrowIfNull(databaseContext, nameof(databaseContext));
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            Logger = logger;
            Timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.GetValue("CookieTracker:Period", 30)));

            DatabaseContext = databaseContext;
            NpgsqlConnection connection = (NpgsqlConnection)DatabaseContext.Database.GetDbConnection();
            connection.Open();
            DatabaseCommands = new Dictionary<DatabaseOperation, NpgsqlCommand>
            {
                [DatabaseOperation.Create] = GetInsertCommand(connection),
                [DatabaseOperation.Read] = GetSelectCommand(connection),
                [DatabaseOperation.Update] = GetUpdateCommand(connection),
                [DatabaseOperation.Delete] = GetDeleteCommand(connection)
            }.ToFrozenDictionary();

            BakingTask = StartBakingAsync();
        }

        public void CreateCookie(Cookie cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie, nameof(cookie));
            UnbakedCookies.Add(cookie.Id, new(cookie, false));
        }

        public ulong Click(Ulid cookieId)
        {
            // Check if the cookie is in the cache. If it isn't, pull it from the database.
            if (!UnbakedCookies.TryGetValue(cookieId, out CachedCookie? unbakedCookie))
            {
                Semaphore.Wait();
                try
                {
                    DbCommand command = DatabaseCommands[DatabaseOperation.Read];
                    command.Parameters[0].Value = cookieId.ToGuid();
                    using DbDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow);
                    if (!reader.Read())
                    {
                        throw new ArgumentException($"No cookie with ID {cookieId} exists.", nameof(cookieId));
                    }

                    unbakedCookie = new(new Cookie()
                    {
                        Id = new Ulid(reader.GetFieldValue<Guid>(0)),
                        Clicks = (ulong)reader.GetFieldValue<decimal>(1)
                    }, true);
                    UnbakedCookies.Add(cookieId, unbakedCookie);
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
            DbCommand createCommand = DatabaseCommands[DatabaseOperation.Create];
            DbCommand updateCommand = DatabaseCommands[DatabaseOperation.Update];
            foreach (DbCommand command in DatabaseCommands.Values)
            {
                await command.PrepareAsync();
            }

            while (await Timer.WaitForNextTickAsync())
            {
                await Semaphore.WaitAsync();
                CachedCookie[] unbakedCookies = UnbakedCookies.Values.ToArray();
                Semaphore.Release();

                List<Guid> updatedCookieIds = new();
                List<decimal> updatedCookieCount = new();
                List<Guid> newCookieIds = new();
                List<decimal> newCookieCount = new();

                // Iterate over a copy of the unbaked cookies dictionary to avoid collection modified exceptions.
                // This foreach loop is used to update the cookies in the database and remove them from cache.
                foreach (CachedCookie cookie in unbakedCookies)
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
                        updatedCookieIds.Add(unbakedCookie.Cookie.Id.ToGuid());
                        updatedCookieCount.Add(unbakedCookie.Cookie.Clicks);
                    }
                    else
                    {
                        newCookieIds.Add(unbakedCookie.Cookie.Id.ToGuid());
                        newCookieCount.Add(unbakedCookie.Cookie.Clicks);
                    }
                }

                if (updatedCookieIds.Count == 0 && newCookieIds.Count == 0)
                {
                    continue;
                }

                await Semaphore.WaitAsync();
                try
                {
                    if (updatedCookieIds.Count != 0)
                    {
                        updateCommand.Parameters[0].Value = updatedCookieIds;
                        updateCommand.Parameters[1].Value = updatedCookieCount;
                        Logger.LogDebug("Updated {Count:N0} cookies!", await updateCommand.ExecuteNonQueryAsync());
                    }

                    if (newCookieIds.Count != 0)
                    {
                        createCommand.Parameters[0].Value = newCookieIds;
                        createCommand.Parameters[1].Value = newCookieCount;
                        Logger.LogDebug("Created {Count:N0} new cookies!", await createCommand.ExecuteNonQueryAsync());
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

        private static NpgsqlCommand GetSelectCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Cookies WHERE Id = @Id LIMIT 1;";
            NpgsqlParameter idParameter = command.CreateParameter();
            idParameter.ParameterName = "@Id";
            idParameter.NpgsqlDbType = NpgsqlDbType.Uuid;
            idParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(idParameter);

            return command;
        }

        private static NpgsqlCommand GetUpdateCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE cookies SET clicks = data_table.clicks FROM (SELECT unnest(ARRAY[@Ids]) AS id, unnest(ARRAY[@Clicks]) AS clicks) AS data_table WHERE cookies.id = data_table.id";

            NpgsqlParameter idsParameter = command.CreateParameter();
            idsParameter.ParameterName = "@Ids";
            idsParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
            idsParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(idsParameter);

            NpgsqlParameter clicksParameter = command.CreateParameter();
            clicksParameter.ParameterName = "@Clicks";
            clicksParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric;
            clicksParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(clicksParameter);

            return command;
        }

        private static NpgsqlCommand GetInsertCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cookies (id, clicks) SELECT * FROM unnest(ARRAY[@Ids], ARRAY[@Clicks]);";

            NpgsqlParameter idsParameter = command.CreateParameter();
            idsParameter.ParameterName = "@Ids";
            idsParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
            idsParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(idsParameter);

            NpgsqlParameter clicksParameter = command.CreateParameter();
            clicksParameter.ParameterName = "@Clicks";
            clicksParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric;
            clicksParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(clicksParameter);

            return command;
        }

        private static NpgsqlCommand GetDeleteCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM cookies WHERE id IN (SELECT * FROM unnest(ARRAY[@Ids]))";

            NpgsqlParameter idsParameter = command.CreateParameter();
            idsParameter.ParameterName = "@Ids";
            idsParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
            idsParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(idsParameter);

            return command;
        }
    }
}
