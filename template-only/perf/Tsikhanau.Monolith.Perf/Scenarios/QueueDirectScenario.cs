using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class QueueDirectScenario : IScenario
{
    public String Name => "queue-direct";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var messages = options.GetInt32("n", 2000);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

        var pingCreator = new PingCreator();
        await QueueDriver.SeedAsync(host, pingCreator, QueueDriver.WarmupMessages, cancellationToken);
        await QueueDriver.DeliverPingsOutboxAsync(host, cancellationToken);
        await QueueDriver.ProcessPongsInboxAsync(host, cancellationToken);
        await QueueDriver.SeedAsync(host, pingCreator, messages, cancellationToken);

        var outbox = await MeasureDrainAsync(
            database,
            "direct-outbox",
            messages,
            () => QueueDriver.DeliverPingsOutboxAsync(host, cancellationToken),
            cancellationToken);

        var inbox = await MeasureDrainAsync(
            database,
            "direct-inbox",
            messages,
            () => QueueDriver.ProcessPongsInboxAsync(host, cancellationToken),
            cancellationToken);

        var eventsPerSecond = messages / (outbox.Seconds + inbox.Seconds);
        Console.WriteLine($"end-to-end sequential (outbox then inbox): {eventsPerSecond:F0} events/s");
    }

    private static async Task<LoadResult> MeasureDrainAsync(
        PerfDatabase database,
        String name,
        Int32 expectedMessages,
        Func<Task<Int32>> drainAsync,
        CancellationToken cancellationToken)
    {
        await database.ResetStatementsAsync(cancellationToken);
        GC.Collect();
        var start = ResourceSnapshot.Take();
        var processedMessages = await drainAsync();
        var usage = ResourceUsage.Since(start);
        if (processedMessages == 0)
        {
            throw new InvalidOperationException($"{name} processed no messages");
        }

        if (processedMessages != expectedMessages)
        {
            Console.WriteLine($"  WARNING processed {processedMessages} expected {expectedMessages}");
        }

        var result = new LoadResult(
            name,
            1,
            processedMessages,
            LatencySummary.Uniform(usage.Elapsed / processedMessages),
            usage);

        Console.WriteLine(result);
        await StatementReport.WriteAsync(database, expectedMessages, cancellationToken);
        return result;
    }
}
