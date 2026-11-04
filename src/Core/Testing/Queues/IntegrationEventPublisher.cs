using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Queues;

internal static class IntegrationEventPublisher
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task PublishAsync(IServiceProvider serviceProvider, IntegrationEvent integrationEvent)
    {
        var integrationEventRegistry = serviceProvider.GetRequiredService<IntegrationEventRegistry>();
        var subscriber = GetFirstSubscriber(serviceProvider, integrationEventRegistry, integrationEvent.GetType());
        var outboxMessage = OutboxMessage.Create(
            integrationEvent,
            integrationEventRegistry,
            integrationEvent.OccurredOn,
            traceParent: null);

        await DeliverAsync(serviceProvider, outboxMessage, subscriber);
    }

    private static ModuleDescriptor GetFirstSubscriber(
        IServiceProvider serviceProvider,
        IntegrationEventRegistry integrationEventRegistry,
        Type eventType)
    {
        var eventName = integrationEventRegistry.GetName(eventType);
        var subscribers = serviceProvider
            .GetRequiredService<SubscriptionRegistry>()
            .GetSubscribers(eventName);

        return subscribers.IsEmpty
            ? throw new InvalidOperationException(
                $"No module subscribes to {eventName} ({eventType.FullName}); "
                + $"a module subscribes with an IIntegrationEventHandler<{eventType.Name}>")
            : subscribers[0];
    }

    private static async Task DeliverAsync(
        IServiceProvider serviceProvider,
        OutboxMessage outboxMessage,
        ModuleDescriptor subscriber)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IEventBus>()
            .DeliverAsync(
                outboxMessage,
                outboxMessage.ContainsSecrets,
                scope.ServiceProvider.ResolveDbContext(subscriber),
                CancellationToken);
    }
}
