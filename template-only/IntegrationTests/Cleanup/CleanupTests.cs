using System.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Cleanup;

public sealed class CleanupTests(PostgresFixture postgresFixture)
{
    private const String RetryFailedRowSql = """
        UPDATE pongs.inbox_messages SET attempts = 0, failed_on = NULL, next_attempt_on = now()
        WHERE id = '{0}' AND failed_on IS NOT NULL
        """;

    private const String DeleteWaitingOnLockSql = """
        SELECT count(*) FROM pg_stat_activity
        WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE 'DELETE%'
        """;

    private static readonly TimeSpan BarrierDeadline = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Processed_messages_older_than_the_window_are_deleted_and_the_rest_stay()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("old");
        await testModulesHost.CreatePingAsync("broken");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(8));
        await testModulesHost.CreatePingAsync("fresh");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

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
    public async Task Failed_messages_older_than_keep_failed_for_are_deleted_and_younger_and_pending_ones_stay()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken-long-ago", "broken-lately"];
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        await testModulesHost.CreatePingAsync("broken-long-ago");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(20));
        await testModulesHost.CreatePingAsync("broken-lately");
        await testModulesHost.CreatePingAsync("flaky");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(11));

        await testModulesHost.RunCleanupAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        inboxRows.Count.ShouldBe(2);
        inboxRows.ShouldContain(row => row.HasFailed && row.Payload.Contains("lately", StringComparison.Ordinal));
        inboxRows.ShouldContain(row => !row.HasFailed && row.Payload.Contains("flaky", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_message_that_a_retry_takes_back_while_cleanup_waits_for_it_stays()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var npgsqlDataSource = NpgsqlDataSource.Create(postgresFixture.ConnectionString);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("broken");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(31));
        var failedRow = (await testModulesHost.ReadInboxRowsAsync()).Single();
        await using var retry = await UncommittedUpdate.ExecuteAsync(
            npgsqlDataSource,
            String.Format(RetryFailedRowSql, failedRow.Id));

        var cleanup = testModulesHost.RunCleanupAsync();
        var isCleanupWaitingForTheRetry = await WaitUntilDeleteWaitsOnLockAsync(testModulesHost, cleanup);
        await retry.CommitAsync();
        await cleanup;

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        isCleanupWaitingForTheRetry.ShouldBeTrue();
        var retriedRow = inboxRows.ShouldHaveSingleItem();
        retriedRow.Id.ShouldBe(failedRow.Id);
        retriedRow.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task Keep_failed_for_out_of_range_stops_the_start_naming_the_setting()
    {
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => TestModulesHost.StartAsync(
                postgresFixture,
                configure: builder => builder.Configuration["App:Cleanup:KeepFailedFor"] = "366.00:00:00"));

        exception.Message.ShouldContain("KeepFailedFor");
    }

    [Fact]
    public async Task Message_waiting_for_a_retry_survives_cleanup()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        await testModulesHost.CreatePingAsync("flaky");
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();
        testModulesHost.Time.Advance(TimeSpan.FromDays(8));

        await testModulesHost.RunCleanupAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        var inboxRow = inboxRows.ShouldHaveSingleItem();

        inboxRow.IsProcessed.ShouldBeFalse();
        inboxRow.HasFailed.ShouldBeFalse();
        inboxRow.Attempts.ShouldBe(1);
    }

    private static async Task<Boolean> WaitUntilDeleteWaitsOnLockAsync(TestModulesHost testModulesHost, Task cleanup)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!cleanup.IsCompleted && stopwatch.Elapsed < BarrierDeadline)
        {
            if (await testModulesHost.Database.ExecuteScalarAsync<Int64>(DeleteWaitingOnLockSql) > 0)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }

        return false;
    }
}
