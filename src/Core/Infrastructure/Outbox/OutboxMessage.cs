using Tsikhanau.Monolith.Infrastructure.Persistence.Naming;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class OutboxMessage : QueueMessage
{
    public const String Table = "outbox_messages";

    internal const String OrderProperty = nameof(OccurredOn);

    internal static String OrderColumn { get; } = ColumnNames.Of(OrderProperty);

    public required DateTimeOffset OccurredOn { get; init; }
}
