using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

public static class ModuleDatabaseExtensions
{
    public static async Task PrepareModuleDatabasesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var modules = serviceProvider.GetRequiredService<ModuleRegistry>().Modules;
        var connectionStrings = DatabaseConnectionStrings.From(serviceProvider.GetRequiredService<IConfiguration>());
        var logins = modules
            .Select(module => connectionStrings.CreateModuleLogin(module.Schema))
            .ToList();

        var maxConnections = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value.MaxConnections;

        await using var adminSession = await AdminSession.OpenLockedAsync(connectionStrings.Admin, cancellationToken);
        await ModuleDatabaseProvisioner.ProvisionAsync(
            adminSession.DataSource,
            logins,
            maxConnections,
            cancellationToken);

        await MigrateAndGrantInboxDeliveryAsync(serviceProvider, modules, cancellationToken);
        await ModuleBoundaryVerifier.VerifyAsync(adminSession.Connection, logins, cancellationToken);
        await adminSession.UnlockAsync(cancellationToken);
    }

    private static async Task MigrateAndGrantInboxDeliveryAsync(
        IServiceProvider serviceProvider,
        IEnumerable<ModuleDescriptor> modules,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        foreach (var module in modules)
        {
            var dbContext = ResolveDbContext(module, scope.ServiceProvider);
            await dbContext.Database.MigrateAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(BuildInboxDeliveryGrantsSql(module.Schema), cancellationToken);
        }
    }

    private static String BuildInboxDeliveryGrantsSql(String schema) =>
        $"""
        GRANT USAGE ON SCHEMA "{schema}" TO "{ModuleLogin.Group}";
        GRANT INSERT, SELECT (id) ON "{schema}".{InboxMessage.Table} TO "{ModuleLogin.Group}";
        """;

    private static DbContext ResolveDbContext(ModuleDescriptor module, IServiceProvider serviceProvider) =>
        (DbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
