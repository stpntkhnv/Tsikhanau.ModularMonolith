using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.Shared.Time;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

public static class ModuleDatabaseExtensions
{
    public static async Task PrepareModuleDatabasesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        serviceProvider
            .GetRequiredService<IStartupValidator>()
            .Validate();

        var settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
        using var deadline = new CancellationTokenSource(
            settings.PrepareTimeout,
            serviceProvider.GetRequiredService<TimeProvider>());

        using var deadlineOrCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            deadline.Token,
            cancellationToken);

        try
        {
            await PrepareAsync(serviceProvider, settings, deadlineOrCancellation.Token);
        }
        catch (OperationCanceledException exception) when (HasRunOutOfTime(deadline, cancellationToken))
        {
            throw new TimeoutException(
                $"Database preparation did not finish within PrepareTimeout ({settings.PrepareTimeout})",
                exception);
        }
    }

    private static async Task PrepareAsync(
        IServiceProvider serviceProvider,
        DatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        var modules = serviceProvider.GetRequiredService<ModuleRegistry>().Modules;
        var connectionStrings = DatabaseConnectionStrings.From(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>());

        var logins = modules
            .Select(module => connectionStrings.CreateModuleLogin(module.Schema))
            .ToList();

        DatabasePreparationLog.WaitingForLock(CreateLogger(serviceProvider));
        await using var adminSession = await AdminSession.OpenLockedAsync(
            connectionStrings.BuildForAdmin(settings.PrepareTimeout),
            cancellationToken);

        await ModuleDatabaseProvisioner.ProvisionAsync(
            adminSession.DataSource,
            logins,
            settings.MaxConnections,
            cancellationToken);

        await MigrateAndGrantInboxDeliveryAsync(serviceProvider, modules, settings.PrepareTimeout, cancellationToken);
        await ModuleBoundaryVerifier.VerifyAsync(adminSession.Connection, logins, cancellationToken);
        await adminSession.UnlockAsync(cancellationToken);
    }

    private static async Task MigrateAndGrantInboxDeliveryAsync(
        IServiceProvider serviceProvider,
        IEnumerable<ModuleDescriptor> modules,
        TimeSpan prepareTimeout,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        foreach (var module in modules)
        {
            var dbContext = ResolveDbContext(module, scope.ServiceProvider);
            dbContext.Database.SetCommandTimeout(prepareTimeout.TotalSecondsRoundedUp);
            await dbContext.Database.MigrateAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(BuildInboxDeliveryGrantsSql(module.Schema), cancellationToken);
        }
    }

    private static Boolean HasRunOutOfTime(CancellationTokenSource deadline, CancellationToken cancellationToken) =>
        deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested;

    private static ILogger CreateLogger(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ModuleDatabaseExtensions));

    private static String BuildInboxDeliveryGrantsSql(String schema) =>
        $"""
        GRANT USAGE ON SCHEMA "{schema}" TO "{ModuleLogin.Group}";
        GRANT INSERT, SELECT (id) ON "{schema}".{InboxMessage.Table} TO "{ModuleLogin.Group}";
        """;

    private static DbContext ResolveDbContext(ModuleDescriptor module, IServiceProvider serviceProvider) =>
        (DbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
