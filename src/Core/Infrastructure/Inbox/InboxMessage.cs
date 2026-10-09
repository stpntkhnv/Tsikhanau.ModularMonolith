using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.Inbox;

public sealed class InboxMessage : QueueMessage
{
    public const String Table = "inbox_messages";

    public required DateTimeOffset OccurredOn { get; init; }

    public required DateTimeOffset ReceivedOn { get; init; }
}
