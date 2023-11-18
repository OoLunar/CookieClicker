using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using OoLunar.CookieClicker.Database;
using OoLunar.CookieClicker.Entities;
using Remora.Rest.Core;

namespace OoLunar.CookieClicker
{
    public sealed class CookieTracker : IAsyncDisposable
    {
        private readonly Dictionary<Ulid, CachedCookie> _cachedCookies = [];
        private readonly NpgsqlConnection _databaseConnection;
        private readonly ILogger<CookieTracker> _logger;
        private readonly SemaphoreSlim _dbConnectionSemaphore = new(1, 1);
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
                [DatabaseOperation.BatchFetch] = GetBatchFetchCommand(_databaseConnection),
                [DatabaseOperation.Update] = GetUpdateCommand(_databaseConnection),
                [DatabaseOperation.Delete] = GetDeleteCommand(_databaseConnection)
            }.ToFrozenDictionary();

            _bakingTask = StartBakingAsync();
        }

        public Cookie CreateCookie(Snowflake guildId, Snowflake channelId, Snowflake messageId)
        {
            Cookie cookie = new(guildId, channelId, messageId);
            _cachedCookies.Add(cookie.Id, new(cookie, false));
            return cookie;
        }

        public decimal Click(Ulid cookieId)
        {
            // Check if the cookie is in the cache. If it isn't, pull it from the database.
            if (!_cachedCookies.TryGetValue(cookieId, out CachedCookie? cachedCookie))
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

                    cachedCookie = new(new Cookie()
                    {
                        Id = new Ulid(reader.GetFieldValue<Guid>(0)),
                        Clicks = reader.GetFieldValue<decimal>(1),
                        GuildId = new Snowflake((ulong)reader.GetInt64(2)),
                        ChannelId = new Snowflake((ulong)reader.GetInt64(3)),
                        MessageId = new Snowflake((ulong)reader.GetInt64(4))
                    }, true);
                    _cachedCookies.Add(cookieId, cachedCookie);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return cachedCookie.Click();
        }

        public Cookie GetCookie(Ulid cookieId)
        {
            // Check if the cookie is in the cache. If it isn't, pull it from the database.
            if (!_cachedCookies.TryGetValue(cookieId, out CachedCookie? cachedCookie))
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

                    cachedCookie = new(new Cookie()
                    {
                        Id = new Ulid(reader.GetFieldValue<Guid>(0)),
                        Clicks = reader.GetFieldValue<decimal>(1),
                        GuildId = new Snowflake(reader.GetFieldValue<ulong>(2)),
                        ChannelId = new Snowflake(reader.GetFieldValue<ulong>(3)),
                        MessageId = new Snowflake(reader.GetFieldValue<ulong>(4))
                    }, true);
                    _cachedCookies.Add(cookieId, cachedCookie);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return cachedCookie.Cookie;
        }

        public bool UpdateCookie(Ulid cookieId, Cookie updatedCookie)
        {
            _semaphore.Wait();
            try
            {
                NpgsqlCommand command = _databaseCommands[DatabaseOperation.Update];
                command.Parameters[0].Value = cookieId.ToGuid();
                command.Parameters[1].Value = updatedCookie.Clicks;
                command.Parameters[2].Value = updatedCookie.ChannelId;
                command.Parameters[3].Value = updatedCookie.MessageId;
                return command.ExecuteNonQuery() == 1;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public IReadOnlyList<Cookie> GetCookies(Snowflake channelId)
        {
            _semaphore.Wait();
            try
            {
                DbCommand command = _databaseCommands[DatabaseOperation.BatchFetch];
                command.Parameters[0].Value = (long)channelId.Value;
                using DbDataReader reader = command.ExecuteReader();
                List<Cookie> cookies = [];
                while (reader.Read())
                {
                    cookies.Add(new Cookie()
                    {
                        Id = new Ulid(reader.GetFieldValue<Guid>(0)),
                        Clicks = reader.GetFieldValue<decimal>(1),
                        GuildId = new Snowflake(reader.GetFieldValue<ulong>(2)),
                        ChannelId = new Snowflake(reader.GetFieldValue<ulong>(3)),
                        MessageId = new Snowflake(reader.GetFieldValue<ulong>(4))
                    });
                }

                return cookies;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task StartBakingAsync()
        {
            InitConnection(null, new StateChangeEventArgs(ConnectionState.Closed, ConnectionState.Closed));
            DbCommand createCommand = _databaseCommands[DatabaseOperation.Create];
            DbCommand updateCommand = _databaseCommands[DatabaseOperation.Update];

            while (await _timer.WaitForNextTickAsync())
            {
                if (_cachedCookies.Count == 0)
                {
                    continue;
                }

                await _semaphore.WaitAsync();
                CachedCookie[] cachedCookies = [.. _cachedCookies.Values];
                _semaphore.Release();

                List<Guid> updatedCookieIds = [];
                List<decimal> updatedCookieCount = [];
                List<Guid> newCookieIds = [];
                List<decimal> newCookieCount = [];

                // Iterate over a copy of the unbaked cookies dictionary to avoid collection modified exceptions.
                // This foreach loop is used to update the cookies in the database and remove them from cache.
                foreach (CachedCookie cookie in cachedCookies)
                {
                    // If the cookie hasn't been interacted with in the last 5 seconds then remove it from the cache.
                    CachedCookie? cachedCookie = cookie;
                    if (cookie.Expired && !_cachedCookies.Remove(cookie.Cookie.Id, out cachedCookie))
                    {
                        cachedCookie = cookie;
                    }

                    // Check if the cookie has been saved before.
                    if (cachedCookie.IsSaved)
                    {
                        updatedCookieIds.Add(cachedCookie.Cookie.Id.ToGuid());
                        updatedCookieCount.Add(cachedCookie.Cookie.Clicks);
                    }
                    else
                    {
                        cachedCookie.IsSaved = true;
                        newCookieIds.Add(cachedCookie.Cookie.Id.ToGuid());
                        newCookieCount.Add(cachedCookie.Cookie.Clicks);
                    }
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
                catch (Exception exception)
                {
                    HttpLogger.BakingError(_logger, exception);
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

            _dbConnectionSemaphore.Wait();

            while (_databaseConnection.FullState is ConnectionState.Closed or ConnectionState.Broken)
            {
                try
                {
                    _databaseConnection.Open();
                    foreach (DbCommand command in _databaseCommands.Values)
                    {
                        command.Prepare();
                    }
                    HttpLogger.DbConnection(_logger, null);
                }
                catch (Exception exception)
                {
                    HttpLogger.DbConnectionError(_logger, exception);
                }
            }

            _dbConnectionSemaphore.Release();
        }

        private static NpgsqlCommand GetSelectCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM cookies WHERE Id = @Id LIMIT 1;";
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
            command.CommandText = "UPDATE cookies SET clicks = data_table.clicks, channel_id = data_table.channel_id, message_id = data_table.message_id FROM (SELECT unnest(ARRAY[@Ids]) AS id, unnest(ARRAY[@Clicks]) AS clicks, unnest(ARRAY[@ChannelId]) AS channel_id, unnest(ARRAY[@MessageId]) AS message_id) AS data_table WHERE cookies.id = data_table.id";

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

            NpgsqlParameter channelIdParameter = command.CreateParameter();
            channelIdParameter.ParameterName = "@ChannelId";
            channelIdParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric;
            channelIdParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(channelIdParameter);

            NpgsqlParameter messageIdParameter = command.CreateParameter();
            messageIdParameter.ParameterName = "@MessageId";
            messageIdParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric;
            messageIdParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(messageIdParameter);

            return command;
        }

        private static NpgsqlCommand GetBatchFetchCommand(NpgsqlConnection connection)
        {
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM cookies WHERE channel_id = @ChannelId;";

            NpgsqlParameter channelIdsParameter = command.CreateParameter();
            channelIdsParameter.ParameterName = "@ChannelId";
            channelIdsParameter.NpgsqlDbType = NpgsqlDbType.Bigint;
            channelIdsParameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(channelIdsParameter);

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
