using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Dictionary<Ulid, CachedCookie> _unbakedCookies = new();
        private readonly NpgsqlConnection _databaseConnection;
        private readonly ILogger<CookieTracker> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly PeriodicTimer _timer;
        private readonly Task _bakingTask;
        private readonly FrozenDictionary<DatabaseOperation, NpgsqlCommand> _databaseCommands;

        public CookieTracker(IConfiguration configuration, ILogger<CookieTracker> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _logger = logger;
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.GetValue("CookieTracker:Period", 30)));

            _databaseConnection = NpgsqlDataSource.Create(CookieDatabaseContext.GetConnectionString(configuration)).CreateConnection();
            _databaseConnection.StateChange += InitConnection;
            _databaseCommands = new Dictionary<DatabaseOperation, NpgsqlCommand>
            {
                [DatabaseOperation.Create] = GetInsertCommand(_databaseConnection),
                [DatabaseOperation.Read] = GetSelectCommand(_databaseConnection),
                [DatabaseOperation.Update] = GetUpdateCommand(_databaseConnection),
                [DatabaseOperation.Delete] = GetDeleteCommand(_databaseConnection)
            }.ToFrozenDictionary();

            _bakingTask = StartBakingAsync();
        }

        public void CreateCookie(Cookie cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie, nameof(cookie));
            _unbakedCookies.Add(cookie.Id, new(cookie, false));
        }

        public ulong Click(Ulid cookieId)
        {
            // Check if the cookie is in the cache. If it isn't, pull it from the database.
            if (!_unbakedCookies.TryGetValue(cookieId, out CachedCookie? unbakedCookie))
            {
                _semaphore.Wait();
                try
                {
                    DbCommand command = _databaseCommands[DatabaseOperation.Read];
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
                    _unbakedCookies.Add(cookieId, unbakedCookie);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            // This method atomically increments the cookie's click count and returns the new value, while also resetting the cookie's timeout.
            return unbakedCookie.Bake();
        }

        private async Task StartBakingAsync()
        {
            InitConnection(null, new StateChangeEventArgs(ConnectionState.Closed, ConnectionState.Closed));
            DbCommand createCommand = _databaseCommands[DatabaseOperation.Create];
            DbCommand updateCommand = _databaseCommands[DatabaseOperation.Update];

            while (await _timer.WaitForNextTickAsync())
            {
                await _semaphore.WaitAsync();
                CachedCookie[] unbakedCookies = _unbakedCookies.Values.ToArray();
                _semaphore.Release();

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
                    if (!_unbakedCookies.Remove(cookie.Cookie.Id, out CachedCookie? unbakedCookie))
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

                await _semaphore.WaitAsync();
                try
                {
                    if (newCookieIds.Count != 0)
                    {
                        createCommand.Parameters[0].Value = newCookieIds;
                        createCommand.Parameters[1].Value = newCookieCount;
                        HttpLogger.CookieCreated(_logger, await createCommand.ExecuteNonQueryAsync(), null);
                    }

                    if (updatedCookieIds.Count != 0)
                    {
                        updateCommand.Parameters[0].Value = updatedCookieIds;
                        updateCommand.Parameters[1].Value = updatedCookieCount;
                        HttpLogger.CookieUpdated(_logger, await updateCommand.ExecuteNonQueryAsync(), null);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _timer.Dispose();
            await _bakingTask;
            await _databaseConnection.DisposeAsync();
            _semaphore.Dispose();
        }

        private void InitConnection(object? sender, StateChangeEventArgs eventArgs)
        {
            if (eventArgs.CurrentState is not ConnectionState.Closed and not ConnectionState.Broken)
            {
                return;
            }

            _semaphore.Wait();
reconnect:
            try
            {
                _databaseConnection.Open();
                foreach (DbCommand command in _databaseCommands.Values)
                {
                    command.Prepare();
                }
            }
            catch (Exception)
            {
                goto reconnect;
            }
            _semaphore.Release();
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
