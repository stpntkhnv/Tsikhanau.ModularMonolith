using Npgsql;
using Respawn;
using Respawn.Graph;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Database;

public sealed class DatabaseResetter
{
    private readonly String _connectionString;
    private readonly Respawner _respawner;

    private DatabaseResetter(String connectionString, Respawner respawner)
    {
        _connectionString = connectionString;
        _respawner = respawner;
    }

    public static async Task<DatabaseResetter> CreateAsync(String connectionString, String[] schemas)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = schemas,
            TablesToIgnore = [new Table(ModuleDbContextOptions.MigrationsHistoryTable)],
        });

        return new DatabaseResetter(connectionString, respawner);
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await _respawner.ResetAsync(connection);
    }
}
