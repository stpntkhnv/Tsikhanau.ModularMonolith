using Dapper;
using Npgsql;

namespace ModularMonolith.Perf.Database;

internal sealed class PerfDatabase : IAsyncDisposable
{
    private const String ReadStatementsSql =
        """
        SELECT s.calls AS Calls, s.mean_exec_time AS MeanMilliseconds,
               regexp_replace(left(s.query, 160), '\s+', ' ', 'g') AS Query, r.rolname AS Role
        FROM pg_stat_statements s
        JOIN pg_database d ON d.oid = s.dbid
        JOIN pg_roles r ON r.oid = s.userid
        WHERE d.datname = current_database()
          AND s.query NOT ILIKE '%pg_stat_statements%'
        ORDER BY s.calls DESC, s.total_exec_time DESC
        """;

    private readonly PerfServer _server;

    private PerfDatabase(PerfServer server, String name)
    {
        _server = server;
        Name = name;
        ConnectionString = BuildConnectionString(server.ConnectionString, name);
    }

    public String ConnectionString { get; }

    private String Name { get; }

    public static async Task<PerfDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var server = await PerfServer.StartAsync(cancellationToken);
        try
        {
            var database = new PerfDatabase(server, $"perf_{Guid.NewGuid():N}"[..21]);
            await ExecuteAsync(server.ConnectionString, $"CREATE DATABASE {database.Name}", cancellationToken);
            await ExecuteAsync(
                database.ConnectionString,
                "CREATE EXTENSION IF NOT EXISTS pg_stat_statements",
                cancellationToken);

            return database;
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    public Task ResetStatementsAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(ConnectionString, "SELECT pg_stat_statements_reset()", cancellationToken);

    public async Task<IReadOnlyList<StatementStatistics>> ReadStatementsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(ConnectionString, cancellationToken);
        var statements = await connection.QueryAsync<StatementStatistics>(
            new CommandDefinition(ReadStatementsSql, cancellationToken: cancellationToken));

        return statements.AsList();
    }

    public async Task<Int64> CountAsync(String sql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(ConnectionString, cancellationToken);
        return await connection.ExecuteScalarAsync<Int64>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(
                _server.ConnectionString,
                $"DROP DATABASE IF EXISTS {Name} WITH (FORCE)",
                CancellationToken.None);
        }
        finally
        {
            await _server.DisposeAsync();
        }
    }

    private static String BuildConnectionString(String serverConnectionString, String name)
    {
        var builder = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name };
        return builder.ConnectionString;
    }

    private static async Task ExecuteAsync(String connectionString, String sql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(connectionString, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task<NpgsqlConnection> OpenAsync(String connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
