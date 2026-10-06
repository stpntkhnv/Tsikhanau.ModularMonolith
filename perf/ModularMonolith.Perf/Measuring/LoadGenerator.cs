using System.Diagnostics;

namespace ModularMonolith.Perf.Measuring;

internal static class LoadGenerator
{
    public static async Task<LoadResult> RunAsync(
        String name,
        Int32 concurrency,
        Int32 operations,
        Func<Int32, Task> operation)
    {
        var latencyTicks = new Int64[operations];
        var nextIndex = -1;
        CollectGarbage();
        var start = ResourceSnapshot.Take();
        var workers = Enumerable
            .Range(0, concurrency)
            .Select(_ => Task.Run(RunWorkerAsync));

        await Task.WhenAll(workers);
        var usage = ResourceUsage.Since(start);
        return new LoadResult(name, concurrency, operations, LatencySummary.FromTimestampTicks(latencyTicks), usage);

        async Task RunWorkerAsync()
        {
            for (var index = Interlocked.Increment(ref nextIndex);
                index < operations;
                index = Interlocked.Increment(ref nextIndex))
            {
                var operationStartedOn = Stopwatch.GetTimestamp();
                await operation(index);
                latencyTicks[index] = Stopwatch.GetTimestamp() - operationStartedOn;
            }
        }
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
