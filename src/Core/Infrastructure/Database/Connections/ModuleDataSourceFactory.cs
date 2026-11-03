using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database.Logins;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.Database.Connections;

internal static class ModuleDataSourceFactory
{
    public static NpgsqlDataSource Create(IServiceProvider serviceProvider, ModuleDescriptor module)
    {
        var connectionStrings = serviceProvider.GetRequiredService<DatabaseConnectionStrings>();
        var settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
        var handlerTimeout = serviceProvider.GetRequiredService<IOptions<QueueSettings>>().Value.HandlerTimeout;
        var moduleCount = serviceProvider.GetRequiredService<ModuleRegistry>().Modules.Count;
        var loginNames = new ModuleLoginNames(connectionStrings.DatabaseName);
        var login = ModuleLogin.Create(loginNames, module.Schema, settings.LoginKey);
        var connectionString = connectionStrings.BuildForModule(
            login,
            settings.CalculatePoolSizePerModule(moduleCount),
            handlerTimeout);

        return new NpgsqlDataSourceBuilder(connectionString)
            .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
            .ConfigureTracing(TraceOnlyWithinAnActivity)
            .Build();
    }

    private static void TraceOnlyWithinAnActivity(NpgsqlTracingOptionsBuilder npgsqlTracingOptionsBuilder) =>
        npgsqlTracingOptionsBuilder
            .ConfigureCommandFilter(_ => HasParentActivity)
            .ConfigureBatchFilter(_ => HasParentActivity);

    private static Boolean HasParentActivity => Activity.Current is not null;
}
