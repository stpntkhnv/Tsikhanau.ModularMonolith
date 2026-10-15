using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Shared.Time;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

public static class ModuleDatabaseExtensions
{
    private const String NeverPrepares =
        $"{DatabaseSettings.PrepareModeSetting} is Never, so nothing may prepare the database: start the host "
        + "with MonolithHost.RunAsync, which checks it through the module logins instead "
        + $"({DatabaseSettings.UpgradeNotes})";

    private const String OnlyStops =
        $"{DatabaseSettings.PrepareModeSetting} is Only, which prepares the database and stops, but this host "
        + "would prepare it and then serve: start the host with MonolithHost.RunAsync, which stops after preparing "
        + $"({DatabaseSettings.UpgradeNotes})";

    public static async Task PrepareModuleDatabasesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var settings = ValidateSettings(serviceProvider);
        EnsurePreparesOnStartup(settings.PrepareMode);
        await PrepareWithinTimeoutAsync(serviceProvider, settings, cancellationToken);
    }

    internal static async Task PrepareModuleDatabasesOnlyAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var settings = ValidateSettings(serviceProvider);
        await PrepareWithinTimeoutAsync(serviceProvider, settings, cancellationToken);
    }

    public static async Task VerifyModuleDatabasesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var settings = ValidateSettings(serviceProvider);
        var connectionStrings = ReadConnectionStrings(serviceProvider);
        var modules = serviceProvider.GetRequiredService<ModuleRegistry>().Modules;

        await using var scope = serviceProvider.CreateAsyncScope();
        foreach (var module in modules)
        {
            await PreparedModuleVerifier.VerifyAsync(
                module,
                serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key),
                ResolveDbContext(module, scope.ServiceProvider),
                settings.MaxConnections,
                modules.Count,
                cancellationToken);
        }

        DatabasePreparationLog.Verified(
            CreateLogger(serviceProvider),
            connectionStrings.DatabaseName,
            JoinNames(modules));
    }

    private static DatabaseSettings ValidateSettings(IServiceProvider serviceProvider)
    {
        serviceProvider
            .GetRequiredService<IStartupValidator>()
            .Validate();

        return serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    }

    private static void EnsurePreparesOnStartup(DatabasePrepareMode prepareMode)
    {
        if (prepareMode == DatabasePrepareMode.Never)
        {
            throw new InvalidOperationException(NeverPrepares);
        }

        if (prepareMode == DatabasePrepareMode.Only)
        {
            throw new InvalidOperationException(OnlyStops);
        }
    }

    private static async Task PrepareWithinTimeoutAsync(
        IServiceProvider serviceProvider,
        DatabaseSettings settings,
        CancellationToken cancellationToken)
    {
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
        var connectionStrings = ReadConnectionStrings(serviceProvider);
        var adminConnectionString = connectionStrings.BuildForAdmin(settings.PrepareTimeout);
        var loginNames = new ModuleLoginNames(connectionStrings.DatabaseName);
        var logins = modules
            .Select(module => ModuleLogin.Create(loginNames, module.Schema, settings.LoginKey))
            .ToList();

        var logger = CreateLogger(serviceProvider);
        DatabasePreparationLog.WaitingForLock(logger);
        await using var adminSession = await AdminSession.OpenLockedAsync(adminConnectionString, cancellationToken);

        await ModuleDatabaseProvisioner.ProvisionAsync(
            adminSession.DataSource,
            loginNames.Group,
            logins,
            settings.MaxConnections,
            cancellationToken);

        await MigrateAndGrantInboxDeliveryAsync(
            serviceProvider,
            modules,
            loginNames.Group,
            settings.PrepareTimeout,
            cancellationToken);

        await ModuleBoundaryVerifier.VerifyAsync(adminSession.Connection, loginNames.Group, logins, cancellationToken);
        await adminSession.UnlockAsync(cancellationToken);
        DatabasePreparationLog.Prepared(logger, connectionStrings.DatabaseName, JoinNames(modules));
    }

    private static async Task MigrateAndGrantInboxDeliveryAsync(
        IServiceProvider serviceProvider,
        IEnumerable<ModuleDescriptor> modules,
        String group,
        TimeSpan prepareTimeout,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        foreach (var module in modules)
        {
            var dbContext = ResolveDbContext(module, scope.ServiceProvider);
            dbContext.Database.SetCommandTimeout(prepareTimeout.TotalSecondsRoundedUp);
            await BoundedConnectionOpener.WarmUpAsync(
                serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key),
                cancellationToken);

            await dbContext.Database.MigrateAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                BuildInboxDeliveryGrantsSql(module.Schema, group),
                cancellationToken);
        }
    }

    private static DatabaseConnectionStrings ReadConnectionStrings(IServiceProvider serviceProvider) =>
        DatabaseConnectionStrings.From(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>());

    private static Boolean HasRunOutOfTime(CancellationTokenSource deadline, CancellationToken cancellationToken) =>
        deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested;

    private static ILogger CreateLogger(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ModuleDatabaseExtensions));

    private static String JoinNames(IEnumerable<ModuleDescriptor> modules) =>
        String.Join(", ", modules.Select(module => module.Name));

    private static String BuildInboxDeliveryGrantsSql(String schema, String group) =>
        $"""
        GRANT USAGE ON SCHEMA "{schema}" TO "{group}";
        GRANT INSERT, SELECT (id) ON "{schema}".{InboxMessage.Table} TO "{group}";
        """;

    private static DbContext ResolveDbContext(ModuleDescriptor module, IServiceProvider serviceProvider) =>
        (DbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
