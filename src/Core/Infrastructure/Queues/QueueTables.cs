using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal static class QueueTables
{
    public static IReadOnlyList<String> Names { get; } =
        [InboxMessage.Table, InternalCommandMessage.Table, OutboxMessage.Table];
}
