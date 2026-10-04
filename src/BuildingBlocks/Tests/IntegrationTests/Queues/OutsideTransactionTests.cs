using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.SendSecret;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class OutsideTransactionTests(PostgresFixture postgresFixture)
{
    private const String SecretSentType = "pings.ping-secret-sent";

    private const String LockSecretRowSql = """
        SELECT id FROM pongs.inbox_messages WHERE type = 'pings.ping-secret-sent' FOR UPDATE NOWAIT
        """;

    private const String LastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE id = @Id";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handler_outside_a_transaction_runs_while_nothing_locks_its_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var isRowLockedDuringHandling = true;
        testModulesHost.PongHooks.WhileHandlingSecret = async _ =>
            isRowLockedDuringHandling = await testModulesHost.Database.IsRowLockedAsync(LockSecretRowSql);

        await SendSecretAsync(testModulesHost, "s3cret");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var row = await ReadSecretRowAsync(testModulesHost);
        isRowLockedDuringHandling.ShouldBeFalse();
        row.IsProcessed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        row.Payload.ShouldBe("{}");
    }

    [Fact]
    public async Task Handler_outside_a_transaction_that_changes_state_fails_for_good()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ChangeStateFor = ["meddle"];
        await SendSecretAsync(testModulesHost, "meddle");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var row = await ReadSecretRowAsync(testModulesHost);
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { row.Id });
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        row.Payload.ShouldContain("meddle");
        lastError.ShouldBe(HandlingFailure.ChangedStateOutsideTransaction.Reason);
    }

    [Fact]
    public async Task Handler_outside_a_transaction_past_the_timeout_is_cancelled_and_retried()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.WhileHandlingSecret = cancellationToken =>
        {
            testModulesHost.Time.Advance(TestModulesHost.HandlerTimeout + TimeSpan.FromSeconds(1));
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        await SendSecretAsync(testModulesHost, "slow");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var failedOn = testModulesHost.Time.GetUtcNow();
        var row = await ReadSecretRowAsync(testModulesHost);
        var lastError = await testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { row.Id });

        row.Attempts.ShouldBe(1);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        lastError.ShouldStartWith("Timed out");
        row.NextAttemptOn.ShouldBe(failedOn.UtcDateTime.AddSeconds(1));
    }

    [Fact]
    public async Task Shutdown_during_a_handler_outside_a_transaction_gives_the_attempt_back()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.WhileHandlingSecret = async cancellationToken =>
        {
            await testModulesHost.PongHooks.Cancellation.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        await SendSecretAsync(testModulesHost, "stop");
        await testModulesHost.DeliverOutboxAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => testModulesHost.ProcessInboxAsync(testModulesHost.PongHooks.Cancellation.Token));

        var stoppedOn = testModulesHost.Time.GetUtcNow();
        var row = await ReadSecretRowAsync(testModulesHost);

        row.Attempts.ShouldBe(0);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe(stoppedOn.UtcDateTime);
    }

    private static async Task<QueueRow> ReadSecretRowAsync(TestModulesHost testModulesHost)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows.Single(row => row.Type == SecretSentType);
    }

    private static async Task SendSecretAsync(TestModulesHost testModulesHost, String secret)
    {
        var pingId = await testModulesHost.CreatePingAsync($"secret-{secret}");
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, secret),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}
