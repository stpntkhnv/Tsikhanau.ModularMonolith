using System.Diagnostics;

namespace ModularMonolith.Perf.Measuring;

internal sealed class StepTimer
{
    private readonly Int64 _startedOn = Stopwatch.GetTimestamp();
    private Int64 _stepStartedOn = Stopwatch.GetTimestamp();

    public TimeSpan Total => Stopwatch.GetElapsedTime(_startedOn);

    public void Mark(String step)
    {
        Console.WriteLine($"  {step,-44} {Stopwatch.GetElapsedTime(_stepStartedOn).TotalMilliseconds,9:F1} ms");
        _stepStartedOn = Stopwatch.GetTimestamp();
    }
}
