using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.TouchPing;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Outbox;

public sealed class OutboxTests(PostgresFixture postgresFixture)
{
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
