using Tsikhanau.Monolith.Infrastructure.Persistence.Naming;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandMessage : QueueMessage
{
    public const String Table = "internal_commands";

    internal const String OrderProperty = nameof(EnqueuedOn);

    internal static String OrderColumn { get; } = ColumnNames.Of(OrderProperty);

    public required DateTimeOffset EnqueuedOn { get; init; }
}
