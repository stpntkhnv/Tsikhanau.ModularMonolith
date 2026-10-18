using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal static class SecondPongHandlerRegistration
{
    public static void AddToPongs(IHostApplicationBuilder builder) =>
        builder.Services
            .AddSingleton<SecondPongHandlerHooks>()
            .AddKeyedScoped<
                IIntegrationEventHandler<PingCreatedIntegrationEvent>,
                SecondPongIntegrationEventHandler>(typeof(IPongsModule));
}
