using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal sealed record QueueSampleRow(Int64 PendingCount, Int64 FailedCount, DateTime? OldestDueOn)
{
    public QueueSample ToSample(String module, String queue, DateTimeOffset sampledOn) =>
        new(module, queue, PendingCount, FailedCount, CalculateOldestDueAge(sampledOn));

    private TimeSpan CalculateOldestDueAge(DateTimeOffset sampledOn) =>
        OldestDueOn is { } oldestDueOn ? sampledOn.UtcDateTime - oldestDueOn : TimeSpan.Zero;
}
