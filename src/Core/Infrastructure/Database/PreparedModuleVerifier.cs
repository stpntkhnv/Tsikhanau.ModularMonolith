using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class PreparedModuleVerifier
{
    public static async Task VerifyAsync(
        ModuleDescriptor module,
        NpgsqlDataSource moduleNpgsqlDataSource,
        DbContext dbContext,
        Int32 maxConnections,
        Int32 moduleCount,
        CancellationToken cancellationToken)
    {
        await using var connection = await ModuleConnection.OpenAsync(
            module,
            moduleNpgsqlDataSource,
            cancellationToken);

        await DatabaseServerVerifier.VerifyAsync(connection, maxConnections, moduleCount, cancellationToken);
        await EnsureNoPendingMigrationsAsync(module, dbContext, cancellationToken);
        EnsureNoModelChanges(module, dbContext);
    }

    private static async Task EnsureNoPendingMigrationsAsync(
        ModuleDescriptor module,
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            throw new InvalidOperationException(
                $"Module {module.Name} has migrations the database does not have: "
                + $"{String.Join(", ", pendingMigrations)}; {DatabaseSettings.RunOnlyFirst}");
        }
    }

    private static void EnsureNoModelChanges(ModuleDescriptor module, DbContext dbContext)
    {
        if (dbContext.Database.HasPendingModelChanges())
        {
            throw new InvalidOperationException(
                $"Module {module.Name} has model changes that no migration holds: add one with "
                + $"bash scripts/add-migration.sh {module.Name} <Name>, then {DatabaseSettings.RunOnlyFirst}");
        }
    }
}
