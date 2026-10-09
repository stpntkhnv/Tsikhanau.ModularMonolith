using BenchmarkDotNet.Running;
using Tsikhanau.Monolith.Perf.Running;

namespace Tsikhanau.Monolith.Perf.Scenarios;

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
