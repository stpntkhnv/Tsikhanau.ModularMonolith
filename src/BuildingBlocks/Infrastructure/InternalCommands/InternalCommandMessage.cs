using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

public sealed class InternalCommandMessage : QueueMessage
{
    public const String Table = "internal_commands";

    public required DateTimeOffset EnqueuedOn { get; init; }
}
