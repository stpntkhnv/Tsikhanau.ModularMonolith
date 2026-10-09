using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class OutboxMessage : QueueMessage
{
    public const String Table = "outbox_messages";

    public required DateTimeOffset OccurredOn { get; init; }
}
