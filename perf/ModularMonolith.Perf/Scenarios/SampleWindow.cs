using ModularMonolith.Perf.Database;
using ModularMonolith.Perf.Measuring;

namespace ModularMonolith.Perf.Scenarios;

internal sealed record SampleWindow(
    TimeSpan EndsAfter,
    Int64 SentCount,
    IReadOnlyDictionary<QueueTable, QueueTableSample> PreviousSamples,
    IReadOnlyDictionary<QueueTable, QueueTableSample> Samples)
{
    public Double GetMillisecondsPerClaim(QueueTable queueTable)
    {
        var claims = Samples[queueTable].Claims - PreviousSamples[queueTable].Claims;
        var milliseconds = Samples[queueTable].ClaimMilliseconds - PreviousSamples[queueTable].ClaimMilliseconds;
        return claims == 0 ? 0 : milliseconds / claims;
    }

    public String BuildLine() =>
        $"  {EndsAfter.TotalSeconds,4:F0} s, sent {SentCount,7}"
            + String.Concat(QueueTable.Watched.Select(BuildTablePart))
            + $", load {MachineLoad.Read()}";

    private String BuildTablePart(QueueTable queueTable)
    {
        var sample = Samples[queueTable];
        var claims = sample.Claims - PreviousSamples[queueTable].Claims;
        var claimedRows = sample.ClaimedRows - PreviousSamples[queueTable].ClaimedRows;
        return $" | {queueTable.Table}: claims {claims,5} x {GetMillisecondsPerClaim(queueTable):F3} ms "
            + $"({claimedRows / (Double)Math.Max(claims, 1):F1} rows), pending {sample.PendingRows,5}, "
            + $"dead {sample.DeadRows,7}, autovacuums {sample.Autovacuums}, "
            + $"indexes {sample.IndexBytes / (1024.0 * 1024.0):F1} MB";
    }
}
