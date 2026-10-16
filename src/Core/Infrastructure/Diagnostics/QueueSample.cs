namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

internal sealed record QueueSample(
    String Module,
    String Queue,
    Int64 PendingCount,
    Int64 FailedCount,
    TimeSpan OldestDueAge)
{
    public KeyValuePair<String, Object?>[] Tags { get; } = QueueTags.Create(Module, Queue);
}
