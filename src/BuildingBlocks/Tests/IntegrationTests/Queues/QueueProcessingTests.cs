using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class QueueProcessingTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_trace_spans_the_command_and_the_other_module_handler()
    {
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource =>
                activitySource.Name is AppDiagnostics.SourceName or "ModularMonolith.Tests",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(activityListener);
        using var testActivitySource = new ActivitySource("ModularMonolith.Tests");
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        ActivityTraceId traceId;
        using (var rootActivity = testActivitySource.StartActivity("root")!)
        {
            traceId = rootActivity.TraceId;
            await testModulesHost.CreatePingAsync("traced");
        }

        await testModulesHost.ProcessQueuesAsync();

        testModulesHost.PongHooks.LastTraceId.Value.ShouldBe(traceId);
    }

    [Fact]
    public async Task Two_pumps_never_take_the_same_message()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        for (var pingNumber = 0; pingNumber < 20; pingNumber++)
        {
            await testModulesHost.CreatePingAsync($"ping-{pingNumber}");
        }

        await testModulesHost.DeliverOutboxAsync();

        var processedCounts = await Task.WhenAll(
            testModulesHost.ProcessInboxAsync(CancellationToken),
            testModulesHost.ProcessInboxAsync(CancellationToken));

        processedCounts
            .Sum()
            .ShouldBe(20);

        testModulesHost.PongHooks.ReceivedPingIds.Count.ShouldBe(20);

        testModulesHost.PongHooks.ReceivedPingIds
            .Distinct()
            .Count()
            .ShouldBe(20);
    }

    [Fact]
    public async Task Background_processor_delivers_without_manual_driving()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, runInBackground: true);

        await testModulesHost.CreatePingAsync("background");

        var stopwatch = Stopwatch.StartNew();
        while (await testModulesHost.CountPongsAsync() == 0)
        {
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
    }
}
