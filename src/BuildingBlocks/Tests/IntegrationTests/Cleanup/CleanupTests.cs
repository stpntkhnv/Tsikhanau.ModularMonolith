using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Cleanup;

public sealed class CleanupTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Processed_messages_older_than_the_window_are_deleted_and_the_rest_stay()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("old");
        await testModulesHost.CreatePingAsync("broken");
        await testModulesHost.ProcessQueuesAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(8));
        await testModulesHost.CreatePingAsync("fresh");
        await testModulesHost.ProcessQueuesAsync();

        var deletedCount = await testModulesHost.RunCleanupAsync();

        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        var outboxRow = outboxRows.ShouldHaveSingleItem();

        deletedCount.ShouldBe(3);
        outboxRow.Payload.ShouldContain("fresh");
        inboxRows.Count.ShouldBe(2);
        inboxRows.ShouldContain(row => row.HasFailed && row.Payload.Contains("broken", StringComparison.Ordinal));
        inboxRows.ShouldContain(row => row.IsProcessed && row.Payload.Contains("fresh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Message_waiting_for_a_retry_survives_cleanup()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        await testModulesHost.CreatePingAsync("flaky");
        await testModulesHost.ProcessQueuesAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(8));

        await testModulesHost.RunCleanupAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        var inboxRow = inboxRows.ShouldHaveSingleItem();

        inboxRow.IsProcessed.ShouldBeFalse();
        inboxRow.HasFailed.ShouldBeFalse();
        inboxRow.Attempts.ShouldBe(1);
    }
}
