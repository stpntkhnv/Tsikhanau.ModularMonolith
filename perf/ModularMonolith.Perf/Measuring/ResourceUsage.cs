using System.Diagnostics;

namespace ModularMonolith.Perf.Measuring;

internal readonly record struct ResourceUsage(
    TimeSpan Elapsed,
    TimeSpan ProcessorTime,
    Int64 AllocatedBytes,
    Int32 Gen0Collections,
    Int32 Gen1Collections,
    Int32 Gen2Collections)
{
    public static ResourceUsage Since(ResourceSnapshot start)
    {
        var elapsed = Stopwatch.GetElapsedTime(start.Timestamp);
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
        return new ResourceUsage(
            elapsed,
            ResourceSnapshot.ReadProcessorTime() - start.ProcessorTime,
            allocatedBytes - start.AllocatedBytes,
            GC.CollectionCount(0) - start.Gen0Collections,
            GC.CollectionCount(1) - start.Gen1Collections,
            GC.CollectionCount(2) - start.Gen2Collections);
    }
}
