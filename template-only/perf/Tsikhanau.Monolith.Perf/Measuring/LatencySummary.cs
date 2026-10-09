using System.Diagnostics;

namespace Tsikhanau.Monolith.Perf.Measuring;

internal readonly record struct LatencySummary(TimeSpan P50, TimeSpan P95, TimeSpan P99, TimeSpan Mean)
{
    public static LatencySummary FromTimestampTicks(Int64[] latencyTicks)
    {
        var sortedMilliseconds = latencyTicks
            .Order()
            .Select(ticks => ticks * 1000.0 / Stopwatch.Frequency)
            .ToList();

        return new LatencySummary(
            TimeSpan.FromMilliseconds(Percentile.Of(sortedMilliseconds, 0.50)),
            TimeSpan.FromMilliseconds(Percentile.Of(sortedMilliseconds, 0.95)),
            TimeSpan.FromMilliseconds(Percentile.Of(sortedMilliseconds, 0.99)),
            TimeSpan.FromMilliseconds(sortedMilliseconds.Average()));
    }

    public static LatencySummary Uniform(TimeSpan latency) => new(latency, latency, latency, latency);
}
