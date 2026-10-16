using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Queues;

internal static class IntegrationEventPublisher
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task PublishAsync(IServiceProvider serviceProvider, IntegrationEvent integrationEvent)
    {
        var eventType = integrationEvent.GetType();
        var integrationEventRegistry = serviceProvider.GetRequiredService<IntegrationEventRegistry>();
        var eventName = integrationEventRegistry.GetName(eventType);

        var subscriber = GetFirstSubscriber(serviceProvider, eventName, eventType);
        await using var scope = serviceProvider.CreateAsyncScope();
        var message = CreateMessage(integrationEvent, eventName, integrationEventRegistry.ContainsSecrets(eventType));
        await scope.ServiceProvider
            .GetRequiredService<IEventBus>()
            .DeliverAsync(
                message,
                message.ContainsSecrets,
                ResolveDbContext(scope.ServiceProvider, subscriber),
                CancellationToken);
    }

    private static ModuleDescriptor GetFirstSubscriber(
        IServiceProvider serviceProvider,
        String eventName,
        Type eventType)
    {
        var subscribers = serviceProvider
            .GetRequiredService<SubscriptionRegistry>()
            .GetSubscribers(eventName);

        return subscribers.IsEmpty
            ? throw new InvalidOperationException(
                $"No module subscribes to {eventName} ({eventType.FullName}); "
                + $"a module subscribes with an IIntegrationEventHandler<{eventType.Name}>")
            : subscribers[0];
    }

    private static OutboxMessage CreateMessage(
        IntegrationEvent integrationEvent,
        String eventName,
        Boolean containsSecrets) =>
        new()
        {
            Id = integrationEvent.Id,
            Type = eventName,
            Payload = MessageSerializer.Serialize(integrationEvent),
            ContainsSecrets = containsSecrets,
            NextAttemptOn = integrationEvent.OccurredOn,
            OccurredOn = integrationEvent.OccurredOn,
        };

    private static ModuleDbContext ResolveDbContext(IServiceProvider serviceProvider, ModuleDescriptor module) =>
        (ModuleDbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
