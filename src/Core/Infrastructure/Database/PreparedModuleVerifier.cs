using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class PreparedModuleVerifier
{
    private const String RunOnlyFirst =
        $"run the image once with {DatabaseSettings.PrepareModeSetting}=Only and the admin connection string, "
        + "then start it again";

    public static async Task VerifyAsync(
        ModuleDescriptor module,
        NpgsqlDataSource moduleNpgsqlDataSource,
        DbContext dbContext,
        Int32 maxConnections,
        Int32 moduleCount,
        CancellationToken cancellationToken)
    {
        await using var connection = await SignInAsync(module, moduleNpgsqlDataSource, cancellationToken);
        await DatabaseServerVerifier.VerifyAsync(connection, maxConnections, moduleCount, cancellationToken);
        await EnsureNoPendingMigrationsAsync(module, dbContext, cancellationToken);
        EnsureNoModelChanges(module, dbContext);
    }

    private static async Task<NpgsqlConnection> SignInAsync(
        ModuleDescriptor module,
        NpgsqlDataSource moduleNpgsqlDataSource,
        CancellationToken cancellationToken)
    {
        try
        {
            return await moduleNpgsqlDataSource.OpenConnectionAsync(cancellationToken);
        }
        catch (PostgresException exception) when (IsRefusedSignIn(exception))
        {
            throw new InvalidOperationException(
                $"Module {module.Name} cannot sign in as {ReadLoginName(moduleNpgsqlDataSource)} "
                + $"({exception.MessageText}): the database is not prepared for this app "
                + $"and its {DatabaseSettings.LoginKeySetting}; {RunOnlyFirst}",
                exception);
        }
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
                + $"{String.Join(", ", pendingMigrations)}; {RunOnlyFirst}");
        }
    }

    private static void EnsureNoModelChanges(ModuleDescriptor module, DbContext dbContext)
    {
        if (dbContext.Database.HasPendingModelChanges())
        {
            throw new InvalidOperationException(
                $"Module {module.Name} has model changes that no migration holds: add one with "
                + $"bash scripts/add-migration.sh {module.Name} <Name>, then {RunOnlyFirst}");
        }
    }

    private static Boolean IsRefusedSignIn(PostgresException exception) =>
        exception.SqlState is PostgresErrorCodes.InvalidPassword
            or PostgresErrorCodes.InvalidAuthorizationSpecification;

    private static String ReadLoginName(NpgsqlDataSource npgsqlDataSource) =>
        new NpgsqlConnectionStringBuilder(npgsqlDataSource.ConnectionString).Username ?? String.Empty;
}
