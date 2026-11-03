using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Infrastructure.Queues.Processing;

namespace Tsikhanau.Monolith.Infrastructure.Platform;

public static class PlatformServiceCollectionExtensions
{
    public const String ConnectionName = DatabaseConnectionStrings.Name;

    public static IHostApplicationBuilder AddPlatform(this IHostApplicationBuilder builder)
    {
        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        return builder;
    }

    public static IHostApplicationBuilder EnsureConnectionString(this IHostApplicationBuilder builder)
    {
        _ = DatabaseConnectionStrings.From(builder.Configuration, builder.Environment);
        return builder;
    }

    public static IServiceCollection AddPlatformServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.TryAddMarker<PlatformServicesMarker>())
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<QueueSignals>();
            services.TryAddSingleton<IEventBus, PostgresEventBus>();
            services.TryAddSingleton(ReadConnectionStrings);
            services.GetOrAddInstance<ModuleRegistry>();
            services.GetOrAddInstance<IntegrationEventRegistry>();
            services.GetOrAddInstance<SubscriptionRegistry>();
            AddSettings<QueueSettings>(services, configuration.GetSection(QueueSettings.Section));
            AddSettings<CleanupSettings>(services, configuration.GetSection(CleanupSettings.Section));
            AddSettings<DatabaseSettings>(services, configuration.GetSection(DatabaseSettings.Section));
            MatchSnakeCaseColumnsInDapper();
        }

        return services;
    }

    private static DatabaseConnectionStrings ReadConnectionStrings(IServiceProvider serviceProvider) =>
        DatabaseConnectionStrings.From(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>());

    private static void AddSettings<TSettings>(IServiceCollection services, IConfigurationSection settingsSection)
        where TSettings : class =>
        services
            .AddOptions<TSettings>()
            .Bind(settingsSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

    private static void MatchSnakeCaseColumnsInDapper() => DefaultTypeMap.MatchNamesWithUnderscores = true;
}

file sealed class PlatformServicesMarker;
