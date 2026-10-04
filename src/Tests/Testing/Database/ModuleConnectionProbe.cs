using Npgsql;
using Tsikhanau.Railway;
using Xunit;

namespace ModularMonolith.Testing.Database;

public sealed class ModuleConnectionProbe(NpgsqlDataSource npgsqlDataSource)
{
    public async Task<Optional<String>> ExecuteAndFindSqlStateAsync(String sql)
    {
        try
        {
            await using var command = npgsqlDataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return Optional<String>.None();
        }
        catch (PostgresException exception)
        {
            return exception.SqlState;
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(String sql)
    {
        await using var command = npgsqlDataSource.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
