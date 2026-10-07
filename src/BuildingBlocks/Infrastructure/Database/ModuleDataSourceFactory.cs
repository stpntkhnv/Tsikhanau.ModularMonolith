using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal static class ModuleDataSourceFactory
{
    public static NpgsqlDataSource Create(IServiceProvider serviceProvider, ModuleDescriptor module)
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>());

        var settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
        var handlerTimeout = serviceProvider.GetRequiredService<IOptions<QueueSettings>>().Value.HandlerTimeout;
        var moduleCount = serviceProvider.GetRequiredService<ModuleRegistry>().Modules.Count;
        var login = connectionStrings.CreateModuleLogin(module.Schema);
        var connectionString = connectionStrings.BuildForModule(
            login,
            $"modularmonolith-{module.Schema}",
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
