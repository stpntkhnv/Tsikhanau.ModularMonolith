using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class PassReportingQueuePump(ModuleQueuePump<IPongsModule> moduleQueuePump)
    : IQueuePump<IPongsModule>
{
    public const String PassMarker = "probe: Pongs pass handled";

    public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
    {
        var handledCount = await moduleQueuePump.RunOnceAsync(cancellationToken);
        await Console.Out.WriteLineAsync($"{PassMarker} {handledCount}");
        return handledCount;
    }
}
