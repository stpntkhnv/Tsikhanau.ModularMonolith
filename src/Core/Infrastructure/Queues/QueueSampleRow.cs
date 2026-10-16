using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal sealed record QueueSampleRow(Int64 PendingCount, Int64 FailedCount, DateTime? OldestDueOn)
{
    public QueueSample ToSample(String module, String queue, DateTimeOffset sampledOn) =>
        new(module, queue, sampledOn, PendingCount, FailedCount, FindOldestDueOn());

    private Optional<DateTimeOffset> FindOldestDueOn() =>
        OldestDueOn is { } oldestDueOn
            ? new DateTimeOffset(DateTime.SpecifyKind(oldestDueOn, DateTimeKind.Utc))
            : Optional<DateTimeOffset>.None();
}
