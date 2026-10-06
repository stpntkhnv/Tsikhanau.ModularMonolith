using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Inbox;

public sealed class InboxTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Subscriber_handles_the_event_and_marks_its_inbox_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("hello");

        await DeliverAndProcessAsync(testModulesHost);

        var pongCount = await testModulesHost.CountPongsAsync();
        var rows = await testModulesHost.ReadInboxRowsAsync();

        pongCount.ShouldBe(1);
        var row = rows.ShouldHaveSingleItem();
        row.IsProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task Throwing_handler_is_retried_on_schedule_then_fails_while_other_messages_flow()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["boom"];
        await testModulesHost.CreatePingAsync("boom");
        await testModulesHost.CreatePingAsync("fine");

        await DeliverAndProcessAsync(testModulesHost);

        var pongCount = await testModulesHost.CountPongsAsync();
        var boomRow = await ReadInboxRowWithLabelAsync(testModulesHost, "boom");
        var failedOn = testModulesHost.Time.GetUtcNow();
        var processedCountBeforeRetry = await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        pongCount.ShouldBe(1);
        boomRow.Attempts.ShouldBe(1);
        boomRow.HasFailed.ShouldBeFalse();
        boomRow.NextAttemptOn.ShouldBe(failedOn.UtcDateTime.AddSeconds(1));
        processedCountBeforeRetry.ShouldBe(0);

        foreach (var seconds in new[] { 1, 10, 60, 300, 900, 1800 })
        {
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(seconds));

            var retriedCount = await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

            retriedCount.ShouldBe(1);
        }

        boomRow = await ReadInboxRowWithLabelAsync(testModulesHost, "boom");
        boomRow.Attempts.ShouldBe(7);
        boomRow.HasFailed.ShouldBeTrue();
        boomRow.IsProcessed.ShouldBeFalse();
    }

    [Fact]
    public async Task Validation_failure_result_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.RejectAsInvalidFor = ["invalid"];
        await testModulesHost.CreatePingAsync("invalid");

        await DeliverAndProcessAsync(testModulesHost);

        var row = await ReadInboxRowWithLabelAsync(testModulesHost, "invalid");

        row.Attempts.ShouldBe(1);
        row.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Unexpected_failure_result_is_retried_on_schedule()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailUnexpectedlyFor = ["glitch"];
        await testModulesHost.CreatePingAsync("glitch");

        await DeliverAndProcessAsync(testModulesHost);

        var failedOn = testModulesHost.Time.GetUtcNow();
        var row = await ReadInboxRowWithLabelAsync(testModulesHost, "glitch");
        var pongCount = await testModulesHost.CountPongsAsync();

        row.Attempts.ShouldBe(1);
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe(failedOn.UtcDateTime.AddSeconds(1));
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Backoff_starts_when_the_handler_fails()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["slow"];
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        };

        await testModulesHost.CreatePingAsync("slow");
        await testModulesHost.DeliverOutboxAsync();
        var startedOn = testModulesHost.Time.GetUtcNow();

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.NextAttemptOn.ShouldBe(startedOn.UtcDateTime.AddSeconds(10 + 1));
        row.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Failure_result_fails_at_once_and_keeps_no_partial_changes()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["reject"];
        await testModulesHost.CreatePingAsync("reject");

        await DeliverAndProcessAsync(testModulesHost);

        var pongCount = await testModulesHost.CountPongsAsync();
        var rows = await testModulesHost.ReadInboxRowsAsync();

        pongCount.ShouldBe(0);
        var row = rows.ShouldHaveSingleItem();
        row.Attempts.ShouldBe(1);
        row.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_event_name_is_retried_then_fails()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertInboxRowAsync(testModulesHost, Guid.CreateVersion7(), "pings.unknown", "{}");

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var rowsAfterFirstAttempt = await testModulesHost.ReadInboxRowsAsync();

        await RetryUntilGivenUpAsync(testModulesHost);

        var rowsAfterLastAttempt = await testModulesHost.ReadInboxRowsAsync();

        var firstAttemptRow = rowsAfterFirstAttempt.ShouldHaveSingleItem();
        firstAttemptRow.HasFailed.ShouldBeFalse();
        firstAttemptRow.Attempts.ShouldBe(1);
        var lastAttemptRow = rowsAfterLastAttempt.ShouldHaveSingleItem();
        lastAttemptRow.HasFailed.ShouldBeTrue();
        lastAttemptRow.Attempts.ShouldBe(7);
    }

    [Fact]
    public async Task Event_this_module_does_not_subscribe_to_is_left_for_an_instance_that_does()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(
            testModulesHost,
            messageId,
            "pings.ping-touched",
            $$"""
            {
                "id": "{{messageId}}",
                "occurredOn": "2026-09-28T08:00:00+00:00",
                "pingId": "{{messageId}}",
                "touches": 1
            }
            """);

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Unreadable_payload_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertInboxRowAsync(
            testModulesHost,
            Guid.CreateVersion7(),
            "pings.ping-created",
            """{"pingId": "not-a-guid"}""");

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancellation_does_not_count_as_an_attempt()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.CancelFor = ["cancel"];
        await testModulesHost.CreatePingAsync("cancel");
        await testModulesHost.DeliverOutboxAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => testModulesHost.ProcessInboxAsync(testModulesHost.PongHooks.Cancellation.Token));

        var rows = await testModulesHost.ReadInboxRowsAsync();
        var pongCount = await testModulesHost.CountPongsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.Attempts.ShouldBe(0);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        pongCount.ShouldBe(0);
    }

    private static async Task DeliverAndProcessAsync(TestModulesHost testModulesHost)
    {
        await testModulesHost.DeliverOutboxAsync();
        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<QueueRow> ReadInboxRowWithLabelAsync(TestModulesHost testModulesHost, String label)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows.Single(row => row.Payload.Contains($"\"{label}\"", StringComparison.Ordinal));
    }

    private static async Task RetryUntilGivenUpAsync(TestModulesHost testModulesHost)
    {
        foreach (var seconds in new[] { 1, 10, 60, 300, 900, 1800 })
        {
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(seconds));
            await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);
        }
    }

    private static Task InsertInboxRowAsync(
        TestModulesHost testModulesHost,
        Guid messageId,
        String type,
        String payload) =>
        testModulesHost.Database.ExecuteAsync(
            """
            INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
            VALUES (@messageId, @type, @payload::jsonb, 0, @receivedOn, @receivedOn, @receivedOn)
            """,
            new { messageId, type, payload, receivedOn = testModulesHost.Time.GetUtcNow() });
}
