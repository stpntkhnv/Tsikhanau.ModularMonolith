using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

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
    public async Task Message_whose_lease_ran_out_is_not_taken_while_its_transaction_still_holds_it()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("held");
        await testModulesHost.DeliverOutboxAsync();
        var processedBySecondPump = -1;
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            testModulesHost.Time.Advance(2 * TestModulesHost.HandlerTimeout + TimeSpan.FromSeconds(1));
            processedBySecondPump = await testModulesHost.ProcessInboxAsync(CancellationToken);
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        processedBySecondPump.ShouldBe(0);
        testModulesHost.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Stale_owner_does_not_lock_a_message_another_instance_claimed_and_gave_back()
    {
        await using var staleInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        await staleInstance.CreatePingAsync("first");
        await staleInstance.CreatePingAsync("second");
        await staleInstance.DeliverOutboxAsync();
        staleInstance.PongHooks.BeforeHandle = async () =>
        {
            staleInstance.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            var otherInboxPump = otherInstance.Services.GetRequiredService<InboxPump<IPongsModule>>();
            var claimedByOther = await otherInboxPump.ClaimBatchAsync(
                TestModulesHost.ClaimBatchSize,
                CancellationToken);

            await otherInboxPump.GiveBackAsync(claimedByOther);
        };

        await staleInstance.ProcessInboxAsync(CancellationToken);

        var rows = await staleInstance.ReadInboxRowsAsync();
        var givenBackRow = rows.Single(row => !row.IsProcessed);

        staleInstance.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
        givenBackRow.Attempts.ShouldBe(1);
        givenBackRow.NextAttemptOn.ShouldBe(
            (otherInstance.Time.GetUtcNow() + TimeSpan.FromMicroseconds(1)).UtcDateTime);
    }

    [Fact]
    public async Task Claim_token_matches_when_the_clock_has_fractions_of_a_microsecond()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.Time.Advance(TimeSpan.FromTicks(1_234_567));
        testModulesHost.PongHooks.FailFor = ["refused"];
        var pingId = await testModulesHost.CreatePingAsync("kept");
        await testModulesHost.CreatePingAsync("refused");
        await testModulesHost.Pings.ExecuteCommandAsync(new SendSecretCommand(pingId, "kept"), CancellationToken);
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var rows = await testModulesHost.ReadInboxRowsAsync();
        var processedCount = rows.Count(row => row.IsProcessed);
        var failedCount = rows.Count(row => row.HasFailed);

        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(row => row.Attempts == 1);
        processedCount.ShouldBe(2);
        failedCount.ShouldBe(1);
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
        lastError.ShouldBe($"Handler.Abandoned: Gave up after {MaxAttempts} attempts, the last did not finish");
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
        lastError.ShouldBe("Handler.Timeout: Timed out after 00:01:00");
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
