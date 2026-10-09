using System.Diagnostics;

namespace Tsikhanau.Monolith.Perf.Measuring;

internal readonly record struct ResourceSnapshot(
    TimeSpan ProcessorTime,
    Int64 AllocatedBytes,
    Int32 Gen0Collections,
    Int32 Gen1Collections,
    Int32 Gen2Collections,
    Int64 Timestamp)
{
    public static ResourceSnapshot Take() =>
        new(
            ReadProcessorTime(),
            GC.GetTotalAllocatedBytes(precise: true),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            Stopwatch.GetTimestamp());

    public static TimeSpan ReadProcessorTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }
}
