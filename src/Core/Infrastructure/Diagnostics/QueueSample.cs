using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

internal sealed record QueueSample(
    String Module,
    String Queue,
    DateTimeOffset SampledOn,
    Int64 PendingCount,
    Int64 FailedCount,
    Optional<DateTimeOffset> OldestDueOn)
{
    public Boolean HasCounts { get; private init; } = true;

    public KeyValuePair<String, Object?>[] Tags { get; } = QueueTags.Create(Module, Queue);

    public static QueueSample CreateWithoutCounts(String module, String queue, DateTimeOffset sampledOn) =>
        new(module, queue, sampledOn, PendingCount: 0, FailedCount: 0, OldestDueOn: sampledOn)
        {
            HasCounts = false,
        };

    public QueueSample AssumeDueSinceSampled() => this with { OldestDueOn = OldestDueOn.Or(SampledOn) };
}
