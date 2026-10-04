using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessage : QueueMessage
{
    public const String Table = "outbox_messages";

    public required DateTimeOffset OccurredOn { get; init; }
}
