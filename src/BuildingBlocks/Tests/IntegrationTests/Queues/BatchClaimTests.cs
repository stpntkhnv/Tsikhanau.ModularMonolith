using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.ScheduleTouch;
using ModularMonolith.TestModules.Pongs;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class BatchClaimTests(PostgresFixture postgresFixture)
{
    private const String DeliveredTouchCountSql = """
        SELECT count(*)::int FROM pings.outbox_messages
        WHERE type = 'pings.ping-touched' AND processed_on IS NOT NULL
        """;

    private const String InsertUnclaimableInboxRowSql = """
        INSERT INTO pings.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@id, 'pongs.unclaimable', '{}'::jsonb, 2147483647, @receivedOn, @receivedOn, @receivedOn)
        """;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Batch_is_claimed_at_once_with_one_lease()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await CreatePingsAsync(testModulesHost, "together", 3);
        await testModulesHost.DeliverOutboxAsync();
        var leaseEnd = testModulesHost.Time.GetUtcNow() + 2 * TestModulesHost.HandlerTimeout;
        IReadOnlyList<QueueRow> rowsSeenFromOutside = [];
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            rowsSeenFromOutside = await testModulesHost.ReadInboxRowsAsync();
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        rowsSeenFromOutside.Count.ShouldBe(3);
        rowsSeenFromOutside.ShouldAllBe(row => row.Attempts == 1 && row.NextAttemptOn == leaseEnd.UtcDateTime);
    }

    [Fact]
    public async Task Message_with_under_one_and_a_half_handler_timeouts_of_lease_goes_back_and_is_claimed_again()
    {
        var handlerTimeout = TimeSpan.FromSeconds(2);
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            handlerTimeout: handlerTimeout);

        await CreatePingsAsync(testModulesHost, "short-lease", 3);
        await testModulesHost.DeliverOutboxAsync();
        var givenBackRows = await ProcessInboxWithASlowFirstHandlerAsync(testModulesHost, TimeSpan.FromSeconds(1.5));
        testModulesHost.Time.Advance(TimeSpan.FromMicroseconds(1));
        var claimedCountInTheNextRun = await ProcessInboxCountingClaimedRowsAsync(testModulesHost);
        var dueOn = TestModulesHost.TestStartedOn + TimeSpan.FromSeconds(1.5) + TimeSpan.FromMicroseconds(1);

        givenBackRows.Count.ShouldBe(2);
        givenBackRows.ShouldAllBe(row => row.Attempts == 0 && row.NextAttemptOn == dueOn.UtcDateTime);
        claimedCountInTheNextRun.ShouldBe(2);
    }

    [Fact]
    public async Task Batch_older_than_the_poll_interval_goes_back_and_is_claimed_again()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await CreatePingsAsync(testModulesHost, "old-batch", 3);
        await testModulesHost.DeliverOutboxAsync();
        var givenBackRows = await ProcessInboxWithASlowFirstHandlerAsync(testModulesHost, TimeSpan.FromSeconds(6));
        testModulesHost.Time.Advance(TimeSpan.FromMicroseconds(1));
        var claimedCountInTheNextRun = await ProcessInboxCountingClaimedRowsAsync(testModulesHost);
        var dueOn = TestModulesHost.TestStartedOn + TimeSpan.FromSeconds(6) + TimeSpan.FromMicroseconds(1);

        givenBackRows.Count.ShouldBe(2);
        givenBackRows.ShouldAllBe(row => row.Attempts == 0 && row.NextAttemptOn == dueOn.UtcDateTime);
        claimedCountInTheNextRun.ShouldBe(2);
    }

    [Theory]
    [InlineData(60, 6)]
    [InlineData(2, 1.5)]
    public async Task Queue_whose_batch_went_back_claims_one_message_at_a_time_for_the_rest_of_the_run(
        Double handlerTimeoutSeconds,
        Double firstHandlerSeconds)
    {
        var handlerTimeout = TimeSpan.FromSeconds(handlerTimeoutSeconds);
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            handlerTimeout: handlerTimeout);

        await using var otherInstance = await TestModulesHost.StartAsync(
            postgresFixture,
            handlerTimeout: handlerTimeout);

        otherInstance.Time.Advance(TimeSpan.FromSeconds(10));
        await CreatePingsAsync(testModulesHost, "one-by-one", 4);
        await testModulesHost.DeliverOutboxAsync();
        var handledCount = 0;
        IReadOnlyList<QueueRow> rowsSeenFromOutside = [];
        IReadOnlyList<InboxMessage> claimedByOther = [];
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            handledCount++;
            if (handledCount == 1)
            {
                testModulesHost.Time.Advance(TimeSpan.FromSeconds(firstHandlerSeconds));
            }

            if (handledCount == 2)
            {
                rowsSeenFromOutside = await testModulesHost.ReadInboxRowsAsync();
                claimedByOther = await otherInstance.Services
                    .GetRequiredService<InboxPump<IPongsModule>>()
                    .ClaimBatchAsync(TestModulesHost.ClaimBatchSize, CancellationToken);
            }
        };

        testModulesHost.Time.AutoAdvanceAmount = TimeSpan.FromMilliseconds(1);

        var processedCount = await testModulesHost.ProcessInboxAsync(CancellationToken);

        var unclaimedCount = rowsSeenFromOutside.Count(row => row.Attempts == 0 && !row.IsProcessed);

        processedCount.ShouldBe(2);
        unclaimedCount.ShouldBe(2);
        claimedByOther.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Batch_given_back_leaves_alone_a_message_another_instance_claimed_meanwhile()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        testModulesHost.PongHooks.CancelFor = ["mine-0", "mine-1"];
        await CreatePingsAsync(testModulesHost, "mine", 2);
        await testModulesHost.DeliverOutboxAsync();
        IReadOnlyList<InboxMessage> claimedByOther = [];
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            claimedByOther = await otherInstance.Services
                .GetRequiredService<InboxPump<IPongsModule>>()
                .ClaimBatchAsync(TestModulesHost.ClaimBatchSize, CancellationToken);
        };

        await Should.ThrowAsync<OperationCanceledException>(
            () => testModulesHost.ProcessInboxAsync(testModulesHost.PongHooks.Cancellation.Token));

        var rows = await testModulesHost.ReadInboxRowsAsync();
        var otherInstanceRow = rows.Single(row => row.Id == claimedByOther.Single().Id);
        var ownRow = rows.Single(row => row.Id != otherInstanceRow.Id);

        otherInstanceRow.Attempts.ShouldBe(2);
        otherInstanceRow.NextAttemptOn.ShouldBe(
            (otherInstance.Time.GetUtcNow() + 2 * TestModulesHost.HandlerTimeout).UtcDateTime);

        ownRow.Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_gives_back_the_rest_of_the_batch()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.CancelFor = ["stop-0", "stop-1", "stop-2"];
        await CreatePingsAsync(testModulesHost, "stop", 3);
        await testModulesHost.DeliverOutboxAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => testModulesHost.ProcessInboxAsync(testModulesHost.PongHooks.Cancellation.Token));

        var rows = await testModulesHost.ReadInboxRowsAsync();

        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(row => row.Attempts == 0 && !row.IsProcessed && !row.HasFailed);
    }

    [Fact]
    public async Task Claim_error_gives_back_the_batches_already_claimed()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await CreatePingsAsync(testModulesHost, "claimed-before", 3);
        await testModulesHost.Database.ExecuteAsync(
            InsertUnclaimableInboxRowSql,
            new { id = Guid.CreateVersion7(), receivedOn = testModulesHost.Time.GetUtcNow() });

        var exception = await Should.ThrowAsync<PostgresException>(() => testModulesHost.RunPingsQueuesAsync());

        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        var deliveredRow = outboxRows.Single(row => row.IsProcessed);
        var givenBackRows = outboxRows
            .Where(row => !row.IsProcessed)
            .ToList();

        exception.SqlState.ShouldBe(PostgresErrorCodes.NumericValueOutOfRange);
        deliveredRow.Attempts.ShouldBe(1);
        givenBackRows.Count.ShouldBe(2);
        givenBackRows.ShouldAllBe(row => row.Attempts == 0 && !row.HasFailed);
    }

    [Fact]
    public async Task Batch_dropped_by_a_crash_comes_back_after_its_lease_and_is_claimed_one_message_at_a_time()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await CreatePingsAsync(testModulesHost, "crashed", 3);
        await testModulesHost.DeliverOutboxAsync();
        await testModulesHost.Services
            .GetRequiredService<InboxPump<IPongsModule>>()
            .ClaimBatchAsync(TestModulesHost.ClaimBatchSize, CancellationToken);

        testModulesHost.Time.Advance(2 * TestModulesHost.HandlerTimeout + TimeSpan.FromSeconds(1));
        var rowsAfterTheCrash = await testModulesHost.ReadInboxRowsAsync();
        var claimedCounts = new List<Int32>();
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            var inboxRows = await testModulesHost.ReadInboxRowsAsync();
            claimedCounts.Add(inboxRows.Count(row => row.Attempts == 2 && !row.IsProcessed));
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();

        rowsAfterTheCrash.ShouldAllBe(row => row.Attempts == 1 && !row.IsProcessed);
        claimedCounts.ShouldBe([1, 1, 1]);
        rows.ShouldAllBe(row => row.Attempts == 2 && row.IsProcessed);
    }

    [Fact]
    public async Task Queue_that_was_empty_is_polled_again_while_its_busy_neighbour_still_has_work()
    {
        const Int32 touchCount = 20;
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await ScheduleTouchesAsync(testModulesHost, touchCount);
        await testModulesHost.DeliverOutboxAsync();
        var startedTouchCount = 0;
        var deliveredTouchCountAtLastTouch = -1;
        testModulesHost.PingHooks.AfterLoad = async () =>
        {
            if (Interlocked.Increment(ref startedTouchCount) == touchCount)
            {
                deliveredTouchCountAtLastTouch =
                    await testModulesHost.Database.ExecuteScalarAsync<Int32>(DeliveredTouchCountSql);
            }
        };

        await testModulesHost.RunPingsQueuesAsync();

        startedTouchCount.ShouldBe(touchCount);
        deliveredTouchCountAtLastTouch.ShouldBeGreaterThan(0);
    }

    private static async Task CreatePingsAsync(TestModulesHost testModulesHost, String labelPrefix, Int32 count)
    {
        for (var pingNumber = 0; pingNumber < count; pingNumber++)
        {
            await testModulesHost.CreatePingAsync($"{labelPrefix}-{pingNumber}");
        }
    }

    private static async Task ScheduleTouchesAsync(TestModulesHost testModulesHost, Int32 count)
    {
        for (var touchNumber = 0; touchNumber < count; touchNumber++)
        {
            var pingId = await testModulesHost.CreatePingAsync($"touched-{touchNumber}");
            var result = await testModulesHost.Pings.ExecuteCommandAsync(
                new ScheduleTouchCommand(pingId),
                CancellationToken);

            result.IsSuccess.ShouldBeTrue();
        }
    }

    private static async Task<IReadOnlyList<QueueRow>> ProcessInboxWithASlowFirstHandlerAsync(
        TestModulesHost testModulesHost,
        TimeSpan firstHandlerDuration)
    {
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            testModulesHost.Time.Advance(firstHandlerDuration);
            return Task.CompletedTask;
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows
            .Where(row => !row.IsProcessed)
            .ToList();
    }

    private static async Task<Int32> ProcessInboxCountingClaimedRowsAsync(TestModulesHost testModulesHost)
    {
        var claimedCount = -1;
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            var rows = await testModulesHost.ReadInboxRowsAsync();
            claimedCount = rows.Count(row => row.Attempts == 1 && !row.IsProcessed);
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        return claimedCount;
    }
}
