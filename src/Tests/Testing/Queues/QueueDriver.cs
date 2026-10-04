using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using Xunit;

namespace ModularMonolith.Testing.Queues;

public static class QueueDriver
{
    private const Int32 MaxPasses = 1000;

    public static async Task ProcessAllAsync(IServiceProvider serviceProvider)
    {
        var queuePumps = serviceProvider
            .GetServices<IQueuePump>()
            .ToList();

        for (var passNumber = 0; passNumber < MaxPasses; passNumber++)
        {
            if (await RunPassAsync(queuePumps) == 0)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Queues did not settle after {MaxPasses} passes");
    }

    private static async Task<Int32> RunPassAsync(IEnumerable<IQueuePump> queuePumps)
    {
        var processedCount = 0;
        foreach (var queuePump in queuePumps)
        {
            processedCount += await queuePump.RunOnceAsync(TestContext.Current.CancellationToken);
        }

        return processedCount;
    }
}
