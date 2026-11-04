using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Persistence.Naming;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class OutboxMessage : QueueMessage
{
    public const String Table = "outbox_messages";

    internal const String OrderProperty = nameof(OccurredOn);

    internal static String OrderColumn { get; } = ColumnNames.Of(OrderProperty);

    public required DateTimeOffset OccurredOn { get; init; }

    internal static OutboxMessage Create(
        IntegrationEvent integrationEvent,
        IntegrationEventRegistry integrationEventRegistry,
        DateTimeOffset nextAttemptOn,
        String? traceParent)
    {
        var eventType = integrationEvent.GetType();
        return new OutboxMessage
        {
            Id = integrationEvent.Id,
            Type = integrationEventRegistry.GetName(eventType),
            Payload = MessageSerializer.Serialize(integrationEvent),
            ContainsSecrets = integrationEventRegistry.ContainsSecrets(eventType),
            TraceParent = traceParent,
            NextAttemptOn = nextAttemptOn,
            OccurredOn = integrationEvent.OccurredOn,
        };
    }
}
