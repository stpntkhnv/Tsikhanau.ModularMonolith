using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class UnitOfWorkScenario : IScenario
{
    private const Int32 WarmupCommits = 5;
    private const Int32 SeedConcurrency = 8;

    public String Name => "unit-of-work";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var seededPings = options.GetInt32("seed", 1000);
        var commits = options.GetInt32("reps", 40);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

        var workload = new CommitWorkload(host.Services, new PingCreator());
        await LoadGenerator.RunAsync(
            "seed",
            SeedConcurrency,
            seededPings,
            _ => workload.CreatePingAsync(cancellationToken));

        foreach (var trackedPings in options.GetInt32List("tracked", "1,100,1000"))
        {
            foreach (var addedEchoes in options.GetInt32List("echoes", "0,50"))
            {
                var samples = await workload.MeasureCommitsAsync(trackedPings, addedEchoes, commits, cancellationToken);
                Console.WriteLine(BuildResultLine(trackedPings, addedEchoes, samples));
            }
        }
    }

    private static String BuildResultLine(Int32 trackedPings, Int32 addedEchoes, IReadOnlyList<CommitSample> samples)
    {
        var sortedMicroseconds = samples
            .Select(sample => sample.Microseconds)
            .Order()
            .ToList();

        var meanAllocatedBytes = samples.Average(sample => sample.AllocatedBytes);
        return $"CommitAsync tracked={trackedPings,4} addedEchoes={addedEchoes,3}: "
            + $"p50 {Percentile.Of(sortedMicroseconds, 0.5),9:F0} us, "
            + $"p95 {Percentile.Of(sortedMicroseconds, 0.95),9:F0} us, "
            + $"alloc {meanAllocatedBytes,10:F0} B, load {MachineLoad.Read()}";
    }

    private sealed record CommitSample(Double Microseconds, Int64 AllocatedBytes);

    private sealed class CommitWorkload(IServiceProvider serviceProvider, PingCreator pingCreator)
    {
        private readonly IPingsModule _pingsModule = serviceProvider.GetRequiredService<IPingsModule>();
        private readonly TimeProvider _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        public Task<Guid> CreatePingAsync(CancellationToken cancellationToken) =>
            pingCreator.CreateAsync(_pingsModule, cancellationToken);

        public async Task<IReadOnlyList<CommitSample>> MeasureCommitsAsync(
            Int32 trackedPings,
            Int32 addedEchoes,
            Int32 commits,
            CancellationToken cancellationToken)
        {
            var samples = new List<CommitSample>();
            for (var commit = -WarmupCommits; commit < commits; commit++)
            {
                var sample = await MeasureCommitAsync(trackedPings, addedEchoes, cancellationToken);
                if (commit >= 0)
                {
                    samples.Add(sample);
                }
            }

            return samples;
        }

        private async Task<CommitSample> MeasureCommitAsync(
            Int32 trackedPings,
            Int32 addedEchoes,
            CancellationToken cancellationToken)
        {
            var pingId = PingId.From(await CreatePingAsync(cancellationToken));
            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ModuleContext<IPingsModule>>().DbContext;
            await dbContext
                .Set<Ping>()
                .OrderBy(ping => ping.Id)
                .Take(trackedPings - 1)
                .ToListAsync(cancellationToken);

            var ping = await dbContext
                .Set<Ping>()
                .SingleAsync(candidate => candidate.Id == pingId, cancellationToken);

            Change(ping, addedEchoes);
            return await MeasureAsync(
                scope.ServiceProvider.GetRequiredService<IUnitOfWork<IPingsModule>>(),
                cancellationToken);
        }

        private void Change(Ping ping, Int32 addedEchoes)
        {
            ping
                .Touch(_timeProvider.GetUtcNow())
                .GetValueOrThrow();

            Enumerable
                .Range(0, addedEchoes)
                .ForEach(echo => AddEcho(ping, echo));
        }

        private static async Task<CommitSample> MeasureAsync(
            IUnitOfWork<IPingsModule> unitOfWork,
            CancellationToken cancellationToken)
        {
            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var commitStartedOn = Stopwatch.GetTimestamp();
            var result = await unitOfWork.CommitAsync(cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(commitStartedOn);
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            result.GetValueOrThrow();
            return new CommitSample(elapsed.TotalMicroseconds, allocatedBytes);
        }

        private static void AddEcho(Ping ping, Int32 echo) =>
            ping
                .AddEcho(EchoId.From(Guid.CreateVersion7()), $"echo {echo}")
                .GetValueOrThrow();
    }
}
