using BenchmarkDotNet.Running;
using ModularMonolith.Perf.Running;

namespace ModularMonolith.Perf.Scenarios;

internal sealed class BenchmarkScenario : IScenario
{
    public String Name => "benchmark";

    public Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(BenchmarkScenario).Assembly)
            .Run([.. options.Arguments]);

        return Task.CompletedTask;
    }
}
