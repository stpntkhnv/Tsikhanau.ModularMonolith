using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Inbox;

public sealed class InboxMessage : QueueMessage
{
    public const String Table = "inbox_messages";

    public required DateTimeOffset OccurredOn { get; init; }

    public required DateTimeOffset ReceivedOn { get; init; }
}
