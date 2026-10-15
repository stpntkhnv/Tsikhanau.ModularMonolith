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
            catch (NpgsqlException exception) when (IsConnectTimeout(exception))
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

    private static Boolean IsConnectTimeout(Exception exception) =>
        exception switch
        {
            NpgsqlException { InnerException: TimeoutException } => true,
            NpgsqlException { InnerException: AggregateException aggregateException } =>
                aggregateException.InnerExceptions.All(IsConnectTimeout),
            _ => false,
        };

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
