using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandMessage : QueueMessage
{
    public const String Table = "internal_commands";

    public required DateTimeOffset EnqueuedOn { get; init; }
}
