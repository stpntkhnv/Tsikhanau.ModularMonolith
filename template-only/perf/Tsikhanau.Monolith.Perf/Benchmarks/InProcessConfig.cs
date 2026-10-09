using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Tsikhanau.Monolith.Perf.Benchmarks;

internal sealed class InProcessConfig : ManualConfig
{
    public InProcessConfig()
    {
        AddJob(
            Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(5)
                .WithIterationCount(15));

        AddDiagnoser(MemoryDiagnoser.Default);
    }
}
