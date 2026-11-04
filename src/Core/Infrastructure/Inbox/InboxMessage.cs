using Tsikhanau.Monolith.Infrastructure.Persistence.Naming;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;

namespace Tsikhanau.Monolith.Infrastructure.Inbox;

public sealed class InboxMessage : QueueMessage
{
    public const String Table = "inbox_messages";

    internal const String OrderProperty = nameof(OccurredOn);

    internal static String OrderColumn { get; } = ColumnNames.Of(OrderProperty);

    public required DateTimeOffset OccurredOn { get; init; }

    public required DateTimeOffset ReceivedOn { get; init; }
}
