using Dapper;
using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed class AdminSession : IAsyncDisposable
{
    private const Int64 PreparationLock = 7_312_604_119_042_731;
    private const String LockSql = "SELECT pg_advisory_lock(@Lock)";
    private const String UnlockSql = "SELECT pg_advisory_unlock(@Lock)";

    private AdminSession(NpgsqlDataSource npgsqlDataSource, NpgsqlConnection connection)
    {
        DataSource = npgsqlDataSource;
        Connection = connection;
    }

    public NpgsqlDataSource DataSource { get; }

    public NpgsqlConnection Connection { get; }

    public static async Task<AdminSession> OpenLockedAsync(
        String adminConnectionString,
        CancellationToken cancellationToken)
    {
        var npgsqlDataSource = NpgsqlDataSource.Create(adminConnectionString);
        NpgsqlConnection connection;
        try
        {
            connection = await BoundedConnectionOpener.OpenAsync(npgsqlDataSource, cancellationToken);
        }
        catch
        {
            await npgsqlDataSource.DisposeAsync();
            throw;
        }

        var session = new AdminSession(npgsqlDataSource, connection);
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

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        await DataSource.DisposeAsync();
    }

    private Task ExecuteAsync(String sql, CancellationToken cancellationToken) =>
        Connection.ExecuteAsync(
            new CommandDefinition(sql, new { Lock = PreparationLock }, cancellationToken: cancellationToken));
}
