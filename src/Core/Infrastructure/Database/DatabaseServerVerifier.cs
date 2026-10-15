using Dapper;
using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class DatabaseServerVerifier
{
    private const String MaxConnectionsSetting =
        $"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}";

    private const String ServerVersionNumberSql = "SELECT current_setting('server_version_num')::int";

    private const String AvailableConnectionsSql = """
        SELECT current_setting('max_connections')::int
             - current_setting('superuser_reserved_connections')::int
             - current_setting('reserved_connections')::int
        """;

    public static async Task VerifyAsync(
        NpgsqlConnection npgsqlConnection,
        Int32 maxConnections,
        Int32 moduleCount,
        CancellationToken cancellationToken)
    {
        PostgresVersion.EnsureSupported(
            await ReadScalarAsync(npgsqlConnection, ServerVersionNumberSql, cancellationToken));

        EnsureConnectionBudget(
            maxConnections,
            moduleCount,
            await ReadScalarAsync(npgsqlConnection, AvailableConnectionsSql, cancellationToken));
    }

    private static void EnsureConnectionBudget(Int32 maxConnections, Int32 moduleCount, Int32 availableConnections)
    {
        var neededConnections = DatabaseSettings.MinConnectionsPerModule * moduleCount;
        if (maxConnections > availableConnections || maxConnections < neededConnections)
        {
            throw new InvalidOperationException(
                $"{MaxConnectionsSetting} is {maxConnections}; it must be at least {neededConnections} "
                + $"({DatabaseSettings.MinConnectionsPerModule} for each of the {moduleCount} modules) "
                + $"and at most the {availableConnections} connections the server leaves to ordinary logins");
        }
    }

    private static Task<Int32> ReadScalarAsync(
        NpgsqlConnection npgsqlConnection,
        String sql,
        CancellationToken cancellationToken) =>
        npgsqlConnection.ExecuteScalarAsync<Int32>(new CommandDefinition(sql, cancellationToken: cancellationToken));
}
