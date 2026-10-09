namespace Tsikhanau.Monolith.Perf.Running;

internal interface IScenario
{
    String Name { get; }

    Task RunAsync(PerfOptions options, CancellationToken cancellationToken);
}
