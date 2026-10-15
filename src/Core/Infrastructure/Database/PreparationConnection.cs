using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed class PreparationConnection : IAsyncDisposable
{
    private PreparationConnection(NpgsqlDataSource npgsqlDataSource, NpgsqlConnection connection)
    {
        DataSource = npgsqlDataSource;
        Connection = connection;
    }

    public NpgsqlDataSource DataSource { get; }

    public NpgsqlConnection Connection { get; }

    public static async Task<PreparationConnection> OpenAsync(
        String connectionString,
        CancellationToken cancellationToken)
    {
        var npgsqlDataSource = NpgsqlDataSource.Create(connectionString);
        try
        {
            return new PreparationConnection(
                npgsqlDataSource,
                await BoundedConnectionOpener.OpenAsync(npgsqlDataSource, cancellationToken));
        }
        catch
        {
            await npgsqlDataSource.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        await DataSource.DisposeAsync();
    }
}
