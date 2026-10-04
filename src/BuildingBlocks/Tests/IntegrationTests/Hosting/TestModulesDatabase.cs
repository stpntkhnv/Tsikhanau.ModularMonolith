using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;

internal static class TestModulesDatabase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<String, DatabaseResetter> Resetters = new(StringComparer.Ordinal);

    public static async Task PrepareAsync(IServiceProvider serviceProvider, String connectionString)
    {
        await Gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await serviceProvider.PrepareModuleDatabasesAsync(TestContext.Current.CancellationToken);

            var databaseResetter = await GetResetterAsync(connectionString);

            await databaseResetter.ResetAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<DatabaseResetter> GetResetterAsync(String connectionString)
    {
        if (Resetters.TryGetValue(connectionString, out var knownResetter))
        {
            return knownResetter;
        }

        var createdResetter = await DatabaseResetter.CreateAsync(
            connectionString,
            [PingsDbContext.SchemaName, PongsDbContext.SchemaName]);

        Resetters[connectionString] = createdResetter;
        return createdResetter;
    }
}
