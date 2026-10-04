using Npgsql;
using Xunit;

namespace ModularMonolith.Testing.Database;

public sealed class UncommittedUpdate : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    private UncommittedUpdate(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<UncommittedUpdate> ExecuteAsync(NpgsqlDataSource npgsqlDataSource, String sql)
    {
        var connection = await npgsqlDataSource.OpenConnectionAsync(CancellationToken);
        try
        {
            var transaction = await connection.BeginTransactionAsync(CancellationToken);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(CancellationToken);
            return new UncommittedUpdate(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task CommitAsync() => _transaction.CommitAsync(CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
