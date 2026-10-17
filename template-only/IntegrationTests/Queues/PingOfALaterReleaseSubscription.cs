using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal static class PingOfALaterReleaseSubscription
{
    public static void AddToPongs(IHostApplicationBuilder builder)
    {
        builder.Services.AddKeyedScoped<
            IIntegrationEventHandler<PingOfALaterReleaseIntegrationEvent>,
            PingOfALaterReleaseIntegrationEventHandler>(typeof(IPongsModule));

        builder.Services
            .GetOrAddInstance<IntegrationEventRegistry>()
            .Register(typeof(PingOfALaterReleaseIntegrationEvent));

        builder.Services
            .GetOrAddInstance<SubscriptionRegistry>()
            .Subscribe(
                PingOfALaterReleaseIntegrationEvent.Name,
                GetPongs(builder.Services),
                runsOutsideTransaction: false);
    }

    private static ModuleDescriptor GetPongs(IServiceCollection services) =>
        services
            .GetOrAddInstance<ModuleRegistry>().Modules
            .Single(module => module.Key == typeof(IPongsModule));
}
