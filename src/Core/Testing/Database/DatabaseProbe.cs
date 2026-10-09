using Dapper;
using Npgsql;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Database;

public sealed class DatabaseProbe(String connectionString)
{
    private const String AwaitedLocksSql = "SELECT count(*) FROM pg_locks WHERE NOT granted";

    public async Task<IReadOnlyList<T>> QueryAsync<T>(String sql, Object? parameters = null)
    {
        await using var connection = await OpenConnectionAsync();
        var rows = await connection.QueryAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));

        return rows.AsList();
    }

    public async Task<T> ExecuteScalarAsync<T>(String sql, Object? parameters = null)
    {
        await using var connection = await OpenConnectionAsync();
        return (await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken)))!;
    }

    public async Task ExecuteAsync(String sql, Object? parameters = null)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));
    }

    public async Task<Boolean> IsRowLockedAsync(String lockSql, Object? parameters = null)
    {
        try
        {
            await ExecuteAsync(lockSql, parameters);
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return true;
        }
    }

    public async Task WaitUntilBlockedOrDoneAsync(Task requestTask)
    {
        while (!requestTask.IsCompleted && await ExecuteScalarAsync<Int64>(AwaitedLocksSql) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    public Task<IReadOnlyList<String>> ReadTableNamesAsync(String schema) =>
        QueryAsync<String>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema",
            new { schema });

    public Task<IReadOnlyList<QueueRow>> ReadQueueRowsAsync(String schema, String table) =>
        QueryAsync<QueueRow>($"""
            SELECT id AS "Id", type AS "Type", attempts AS "Attempts", processed_on IS NOT NULL AS "IsProcessed",
                   failed_on IS NOT NULL AS "HasFailed", next_attempt_on AS "NextAttemptOn", payload::text AS "Payload"
            FROM "{schema}".{table}
            ORDER BY next_attempt_on, id
            """);

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
