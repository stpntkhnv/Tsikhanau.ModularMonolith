using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Features.TouchPing;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Outbox;

public sealed class OutboxTests(PostgresFixture postgresFixture)
{
    private const String ReadOutboxLastErrorSql = "SELECT last_error FROM pings.outbox_messages";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Created_ping_leaves_its_integration_event_in_the_outbox()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var pingId = await testModulesHost.CreatePingAsync("outbound");

        var rows = await testModulesHost.ReadOutboxRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.Type.ShouldBe("pings.ping-created");
        row.IsProcessed.ShouldBeFalse();
        row.Payload.ShouldContain(pingId.ToString());
    }

    [Fact]
    public async Task Delivery_fills_the_subscriber_inbox_and_marks_the_outbox_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("delivered");

        var processedCount = await testModulesHost.DeliverOutboxAsync();

        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        processedCount.ShouldBe(1);
        var outboxRow = outboxRows.ShouldHaveSingleItem();
        outboxRow.IsProcessed.ShouldBeTrue();
        var inboxRow = inboxRows.ShouldHaveSingleItem();
        inboxRow.Id.ShouldBe(outboxRow.Id);
        inboxRow.IsProcessed.ShouldBeFalse();
    }

    [Fact]
    public async Task Delivering_the_same_message_twice_keeps_one_inbox_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("twice");
        await testModulesHost.DeliverOutboxAsync();
        await testModulesHost.Database.ExecuteAsync(
            "UPDATE pings.outbox_messages SET processed_on = NULL, next_attempt_on = @Now",
            new { Now = testModulesHost.Time.GetUtcNow() });

        var processedCount = await testModulesHost.DeliverOutboxAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();
        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();

        processedCount.ShouldBe(1);
        inboxRows.ShouldHaveSingleItem();
        var outboxRow = outboxRows.ShouldHaveSingleItem();
        outboxRow.IsProcessed.ShouldBeTrue();
        outboxRow.Attempts.ShouldBe(2);
        outboxRow.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task Failure_at_the_commit_after_the_inbox_insert_and_the_mark_rolls_back_both()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("refused mark");
        var failedOn = testModulesHost.Time.GetUtcNow();
        await using var processedMarkRefusal = await RowChangeRefusal.InstallAsync(
            testModulesHost.Database,
            PingsDbContext.SchemaName,
            OutboxMessage.Table,
            "UPDATE",
            RowChangeRefusal.ProcessedMark);

        await testModulesHost.DeliverOutboxAsync();

        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(ReadOutboxLastErrorSql);
        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        inboxRows.ShouldBeEmpty();
        var outboxRow = outboxRows.ShouldHaveSingleItem();
        outboxRow.IsProcessed.ShouldBeFalse();
        outboxRow.HasFailed.ShouldBeFalse();
        outboxRow.Attempts.ShouldBe(1);
        outboxRow.NextAttemptOn.ShouldBe(failedOn.UtcDateTime.AddSeconds(1));
        lastError.ShouldContain(RowChangeRefusal.Message);
    }

    [Fact]
    public async Task Event_type_this_instance_does_not_know_is_left_for_an_instance_that_does()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        var publishedOn = testModulesHost.Time.GetUtcNow();
        await testModulesHost.Database.ExecuteAsync(
            """
            INSERT INTO pings.outbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on)
            VALUES (@messageId, 'pings.ping-from-a-newer-release', @payload::jsonb, 0, @publishedOn, @publishedOn)
            """,
            new { messageId, payload = $$"""{"id": "{{messageId}}"}""", publishedOn });

        await testModulesHost.DeliverOutboxAsync();

        var rows = await testModulesHost.ReadOutboxRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.Attempts.ShouldBe(1);
        row.NextAttemptOn.ShouldBe(publishedOn.UtcDateTime.AddSeconds(1));
    }

    [Fact]
    public async Task Event_nobody_subscribes_to_is_still_marked_processed()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("lonely");
        await testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken);

        var processedCount = await testModulesHost.DeliverOutboxAsync();

        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        processedCount.ShouldBe(2);
        var touchedRow = outboxRows.Single(row => row.Type == "pings.ping-touched");
        touchedRow.IsProcessed.ShouldBeTrue();
        inboxRows.ShouldAllBe(row => row.Type == "pings.ping-created");
    }
}
