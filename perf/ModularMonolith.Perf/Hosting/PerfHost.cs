using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.TestModules.Pings.Infrastructure;
using ModularMonolith.TestModules.Pongs.Infrastructure;

namespace ModularMonolith.Perf.Hosting;

internal static class PerfHost
{
    public static IHost Build(String connectionString, PerfHostSettings settings)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });

        Configure(builder, connectionString, settings);
        ConfigureLogging(builder.Logging, settings);
        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.Services.AddPingsInfrastructure();
        builder.Services.AddPongsInfrastructure();

        return builder.Build();
    }

    public static async Task<IHost> StartAsync(
        String connectionString,
        PerfHostSettings settings,
        CancellationToken cancellationToken)
    {
        var host = Build(connectionString, settings);
        try
        {
            await host.Services.PrepareModuleDatabasesAsync(cancellationToken);
            await host.StartAsync(cancellationToken);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private static void Configure(
        HostApplicationBuilder builder,
        String connectionString,
        PerfHostSettings settings)
    {
        var configuration = builder.Configuration;
        configuration[$"ConnectionStrings:{PlatformServiceCollectionExtensions.ConnectionName}"] = connectionString;
        configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.RunInBackground)}"] =
            settings.RunInBackground.ToString();

        configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.PollInterval)}"] =
            settings.PollInterval.ToString("c", CultureInfo.InvariantCulture);

        configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.HandlerTimeout)}"] = "00:01:00";
        configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.RunInBackground)}"] = "false";
        configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.Interval)}"] = "01:00:00";
        configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.KeepFor)}"] = "7.00:00:00";
        configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}"] =
            settings.MaxConnections.ToString(CultureInfo.InvariantCulture);
    }

    private static void ConfigureLogging(ILoggingBuilder logging, PerfHostSettings settings)
    {
        logging.ClearProviders();
        if (settings.LogsToConsole)
        {
            logging
                .AddSimpleConsole()
                .SetMinimumLevel(LogLevel.Information);
        }
    }
}
