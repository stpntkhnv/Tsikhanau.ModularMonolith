using System.Diagnostics;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class ClaimTests(PostgresFixture postgresFixture)
{
    private const Int32 MaxAttempts = 7;

    private const String LastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE id = @Id";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Attempt_and_lease_are_committed_before_the_handler_runs()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("watched");
        await testModulesHost.DeliverOutboxAsync();
        var claimedOn = testModulesHost.Time.GetUtcNow();
        QueueRow? rowSeenFromOutside = null;
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            var rows = await testModulesHost.ReadInboxRowsAsync();
            rowSeenFromOutside = rows.Single();
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        rowSeenFromOutside.ShouldNotBeNull();
        rowSeenFromOutside.Attempts.ShouldBe(1);
        rowSeenFromOutside.NextAttemptOn.ShouldBe(claimedOn.UtcDateTime + 2 * TestModulesHost.HandlerTimeout);
    }

    [Fact]
    public async Task Handler_that_kills_its_connection_is_counted_and_finally_fails()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.TerminateConnectionFor = ["poison"];
        await testModulesHost.CreatePingAsync("poison");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);
        var firstRow = (await testModulesHost.ReadInboxRowsAsync()).Single();
        foreach (var seconds in new[] { 1, 10, 60, 300, 900, 1800 })
        {
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(seconds));
            await testModulesHost.ProcessInboxAsync(CancellationToken);
        }

        var lastRow = (await testModulesHost.ReadInboxRowsAsync()).Single();

        firstRow.Attempts.ShouldBe(1);
        firstRow.HasFailed.ShouldBeFalse();
        lastRow.Attempts.ShouldBe(MaxAttempts);
        lastRow.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Message_claimed_more_times_than_allowed_fails_without_running()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("crashed");
        await testModulesHost.DeliverOutboxAsync();
        await testModulesHost.Database.ExecuteAsync(
            "UPDATE pongs.inbox_messages SET attempts = @Attempts",
            new { Attempts = MaxAttempts });

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var row = (await testModulesHost.ReadInboxRowsAsync()).Single();
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { row.Id });
        var pongCount = await testModulesHost.CountPongsAsync();

        row.HasFailed.ShouldBeTrue();
        row.IsProcessed.ShouldBeFalse();
        lastError.ShouldBe($"Gave up after {MaxAttempts} attempts, the last did not finish");
        pongCount.ShouldBe(0);
        testModulesHost.PongHooks.ReceivedPingIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handler_slower_than_the_timeout_fails_and_is_retried()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            testModulesHost.Time.Advance(TestModulesHost.HandlerTimeout + TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        };

        await testModulesHost.CreatePingAsync("slow");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var failedOn = testModulesHost.Time.GetUtcNow();
        var row = (await testModulesHost.ReadInboxRowsAsync()).Single();
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { row.Id });
        var pongCount = await testModulesHost.CountPongsAsync();

        row.Attempts.ShouldBe(1);
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe(failedOn.UtcDateTime.AddSeconds(1));
        lastError.ShouldStartWith("Timed out");
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Database_ends_a_transaction_that_outlives_the_timeout_even_if_the_handler_ignores_it()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            handlerTimeout: TimeSpan.FromSeconds(2));

        testModulesHost.PongHooks.KeepBusyFor = ["stubborn"];
        await testModulesHost.CreatePingAsync("stubborn");
        await testModulesHost.DeliverOutboxAsync();
        var stopwatch = Stopwatch.StartNew();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var row = (await testModulesHost.ReadInboxRowsAsync()).Single();
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { row.Id });

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.Attempts.ShouldBe(1);
        lastError.ShouldNotBeNullOrWhiteSpace();
    }
}
