using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class BoundedConnectionOpener
{
    public static async Task<NpgsqlConnection> OpenAsync(
        NpgsqlDataSource npgsqlDataSource,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var connection = npgsqlDataSource.CreateConnection();
            var opening = connection.OpenAsync(cancellationToken);
            try
            {
                await opening.WaitAsync(cancellationToken);
                return connection;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _ = DisposeWhenOpenEndsAsync(connection, opening);
                throw;
            }
            catch (NpgsqlException exception) when (exception.InnerException is TimeoutException)
            {
                await connection.DisposeAsync();
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    public static async Task WarmUpAsync(NpgsqlDataSource npgsqlDataSource, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(npgsqlDataSource, cancellationToken);
    }

    private static async Task DisposeWhenOpenEndsAsync(NpgsqlConnection connection, Task opening)
    {
        try
        {
            await opening;
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException)
        {
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
