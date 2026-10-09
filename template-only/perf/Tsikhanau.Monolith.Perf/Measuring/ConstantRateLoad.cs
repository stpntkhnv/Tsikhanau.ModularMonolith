using System.Diagnostics;

namespace Tsikhanau.Monolith.Perf.Measuring;

internal sealed class ConstantRateLoad(Int32 operationsPerSecond, TimeSpan duration)
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(10);

    private Int64 _sentCount;

    public Int64 SentCount => Interlocked.Read(ref _sentCount);

    public async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        var startedOn = Stopwatch.GetTimestamp();
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var elapsed = Stopwatch.GetElapsedTime(startedOn);
            if (elapsed >= duration)
            {
                return;
            }

            var dueCount = (Int64)(elapsed.TotalSeconds * operationsPerSecond) - SentCount;
            for (var sent = 0L; sent < dueCount; sent++)
            {
                await operation(cancellationToken);
                Interlocked.Increment(ref _sentCount);
            }
        }
    }
}
