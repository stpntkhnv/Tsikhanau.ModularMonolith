using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class QueueProcessingTests(PostgresFixture postgresFixture)
{
    private static readonly TimeSpan BarrierDeadline = TimeSpan.FromSeconds(30);

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
    public async Task Processing_queues_reports_a_message_that_keeps_failing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("broken");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => testModulesHost.ProcessQueuesAsync());

        exception.Message.ShouldContain("Pongs inbox_messages pings.ping-created after 1 attempts: Pong.Rejected");
    }

    [Fact]
    public async Task Processing_queues_reports_a_message_waiting_for_a_retry()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        await testModulesHost.CreatePingAsync("flaky");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => testModulesHost.ProcessQueuesAsync());

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        var inboxRow = inboxRows.ShouldHaveSingleItem();

        exception.Message.ShouldContain("Pongs inbox_messages pings.ping-created after 1 attempts: Pong handler");
        inboxRow.IsProcessed.ShouldBeFalse();
        inboxRow.HasFailed.ShouldBeFalse();
        inboxRow.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Processing_queues_allowing_failures_leaves_the_failed_message_to_the_test()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("broken");

        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        inboxRows
            .ShouldHaveSingleItem().HasFailed
            .ShouldBeTrue();
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
        var claimedCountTask = HoldFirstTwoHandlersUntilBothRunAsync(testModulesHost);

        var processedCounts = await Task.WhenAll(
            testModulesHost.ProcessInboxAsync(CancellationToken),
            testModulesHost.ProcessInboxAsync(CancellationToken));

        var claimedCountWhileBothHandle = await claimedCountTask.WaitAsync(BarrierDeadline, CancellationToken);

        claimedCountWhileBothHandle.ShouldBe(20);
        processedCounts.ShouldAllBe(processedCount => processedCount > 0);
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

    private static Task<Int32> HoldFirstTwoHandlersUntilBothRunAsync(TestModulesHost testModulesHost)
    {
        var arrivedCount = 0;
        var claimedCount = new TaskCompletionSource<Int32>(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            var arrivedNumber = Interlocked.Increment(ref arrivedCount);
            if (arrivedNumber == 2)
            {
                var rows = await testModulesHost.ReadInboxRowsAsync();
                claimedCount.SetResult(rows.Count(row => row.Attempts == 1 && !row.IsProcessed));
            }

            if (arrivedNumber <= 2)
            {
                await claimedCount.Task.WaitAsync(BarrierDeadline, CancellationToken);
            }
        };

        return claimedCount.Task;
    }
}
