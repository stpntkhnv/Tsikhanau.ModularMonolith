using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.Perf.Measuring;
using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pongs;

namespace ModularMonolith.Perf.Scenarios;

internal static class QueueDriver
{
    public const Int32 WarmupMessages = 200;

    private const Int32 SeedConcurrency = 8;

    public static Task<LoadResult> SeedAsync(
        IHost host,
        PingCreator pingCreator,
        Int32 messages,
        CancellationToken cancellationToken)
    {
        var pingsModule = host.Services.GetRequiredService<IPingsModule>();
        return LoadGenerator.RunAsync(
            "seed",
            SeedConcurrency,
            messages,
            _ => pingCreator.CreateAsync(pingsModule, cancellationToken));
    }

    public static Task<Int32> DeliverPingsOutboxAsync(IHost host, CancellationToken cancellationToken) =>
        host.Services
            .GetRequiredService<OutboxPump<IPingsModule>>()
            .RunOnceAsync(cancellationToken);

    public static Task<Int32> ProcessPongsInboxAsync(IHost host, CancellationToken cancellationToken) =>
        host.Services
            .GetRequiredService<InboxPump<IPongsModule>>()
            .RunOnceAsync(cancellationToken);
}
