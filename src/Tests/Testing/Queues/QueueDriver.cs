using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Xunit;

namespace ModularMonolith.Testing.Queues;

public static class QueueDriver
{
    private const Int32 MaxPasses = 1000;

    public static async Task ProcessAllAsync(IServiceProvider serviceProvider)
    {
        await ProcessAllAllowingFailuresAsync(serviceProvider);
        var unsettledMessages = await UnsettledMessages.ReadAsync(serviceProvider);
        if (unsettledMessages.Count > 0)
        {
            throw new InvalidOperationException(
                $"Queues hold failed or retrying messages: {String.Join("; ", unsettledMessages)}. "
                + "A test that expects a failure processes queues with ProcessQueuesAllowingFailuresAsync");
        }
    }

    public static async Task ProcessAllAllowingFailuresAsync(IServiceProvider serviceProvider)
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
