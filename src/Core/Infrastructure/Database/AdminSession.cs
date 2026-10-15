using Dapper;
using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed class AdminSession : IAsyncDisposable
{
    private const Int64 PreparationLock = 7_312_604_119_042_731;
    private const String LockSql = "SELECT pg_advisory_lock(@Lock)";
    private const String UnlockSql = "SELECT pg_advisory_unlock(@Lock)";

    private readonly PreparationConnection _preparationConnection;

    private AdminSession(PreparationConnection preparationConnection)
    {
        _preparationConnection = preparationConnection;
    }

    public NpgsqlDataSource DataSource => _preparationConnection.DataSource;

    public NpgsqlConnection Connection => _preparationConnection.Connection;

    public static async Task<AdminSession> OpenLockedAsync(
        String adminConnectionString,
        CancellationToken cancellationToken)
    {
        var session = new AdminSession(
            await PreparationConnection.OpenAsync(adminConnectionString, cancellationToken));

        try
        {
            await session.ExecuteAsync(LockSql, cancellationToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public Task UnlockAsync(CancellationToken cancellationToken) => ExecuteAsync(UnlockSql, cancellationToken);

    public ValueTask DisposeAsync() => _preparationConnection.DisposeAsync();

    private Task ExecuteAsync(String sql, CancellationToken cancellationToken) =>
        Connection.ExecuteAsync(
            new CommandDefinition(sql, new { Lock = PreparationLock }, cancellationToken: cancellationToken));
}
