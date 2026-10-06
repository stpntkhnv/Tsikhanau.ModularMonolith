using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.SendSecret;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class OutsideTransactionTests(PostgresFixture postgresFixture)
{
    private const String SecretSentType = "pings.ping-secret-sent";

    private const String LockSecretRowSql = """
        SELECT id FROM pongs.inbox_messages WHERE type = 'pings.ping-secret-sent' FOR UPDATE NOWAIT
        """;

    private const String LastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE id = @Id";

    private const String HoldStuckRowSql = """
        UPDATE pongs.inbox_messages SET last_error = last_error WHERE payload->>'secret' = 'stuck'
        """;

    private static readonly TimeSpan BarrierDeadline = TimeSpan.FromSeconds(30);

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
        row.NextAttemptOn.ShouldBe((stoppedOn + TimeSpan.FromMicroseconds(1)).UtcDateTime);
    }

    [Fact]
    public async Task Failure_write_to_a_row_held_by_someone_else_times_out_and_the_rest_of_the_batch_goes_back()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var npgsqlDataSource = NpgsqlDataSource.Create(postgresFixture.ConnectionString);
        var rowHeld = new TaskCompletionSource<UncommittedUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.FailFor = ["stuck"];
        testModulesHost.PongHooks.WhileHandlingSecret = async _ =>
            rowHeld.SetResult(await UncommittedUpdate.ExecuteAsync(npgsqlDataSource, HoldStuckRowSql));

        var pingId = await testModulesHost.CreatePingAsync("holder");
        testModulesHost.Time.Advance(TimeSpan.FromMilliseconds(1));
        await SendSecretToAsync(testModulesHost, pingId, "stuck");
        testModulesHost.Time.Advance(TimeSpan.FromMilliseconds(1));
        await SendSecretToAsync(testModulesHost, pingId, "later-1");
        await SendSecretToAsync(testModulesHost, pingId, "later-2");
        await testModulesHost.DeliverOutboxAsync();
        var stopwatch = Stopwatch.StartNew();

        var processing = Should.ThrowAsync<Exception>(
            () => testModulesHost.ProcessInboxAsync(CancellationToken));

        await using var heldRow = await rowHeld.Task.WaitAsync(BarrierDeadline, CancellationToken);
        var exception = await processing;
        var laterRows = await ReadSecretRowsAsync(testModulesHost, "later");

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
        exception
            .GetBaseException()
            .ShouldBeOfType<TimeoutException>();

        laterRows.Count.ShouldBe(2);
        laterRows.ShouldAllBe(row => row.Attempts == 0 && !row.IsProcessed && !row.HasFailed);
    }

    [Fact]
    public async Task Stale_owner_does_not_mark_a_message_another_instance_claimed_and_gave_back()
    {
        await using var staleInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        var givenBackOn = LetOtherInstanceTakeOverDuringHandling(staleInstance, otherInstance);
        await SendSecretAsync(staleInstance, "stale");
        await staleInstance.DeliverOutboxAsync();

        await staleInstance.ProcessInboxAsync(CancellationToken);

        var row = await ReadSecretRowAsync(staleInstance);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.Attempts.ShouldBe(1);
        row.NextAttemptOn.ShouldBe((givenBackOn + TimeSpan.FromMicroseconds(1)).UtcDateTime);
    }

    [Fact]
    public async Task Stale_owner_does_not_record_a_failure_on_a_message_another_instance_claimed_and_gave_back()
    {
        await using var staleInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        var givenBackOn = LetOtherInstanceTakeOverDuringHandling(staleInstance, otherInstance);
        staleInstance.PongHooks.FailFor = ["stale"];
        await SendSecretAsync(staleInstance, "stale");
        await staleInstance.DeliverOutboxAsync();

        await staleInstance.ProcessInboxAsync(CancellationToken);

        var row = await ReadSecretRowAsync(staleInstance);
        var lastError = await staleInstance.Database.ExecuteScalarAsync<String?>(LastErrorSql, new { row.Id });
        row.HasFailed.ShouldBeFalse();
        row.Attempts.ShouldBe(1);
        row.NextAttemptOn.ShouldBe((givenBackOn + TimeSpan.FromMicroseconds(1)).UtcDateTime);
        lastError.ShouldBeNull();
    }

    private static DateTimeOffset LetOtherInstanceTakeOverDuringHandling(
        TestModulesHost staleInstance,
        TestModulesHost otherInstance)
    {
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        otherInstance.PongHooks.WhileHandlingSecret = async cancellationToken =>
        {
            await otherInstance.PongHooks.Cancellation.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        staleInstance.PongHooks.WhileHandlingSecret = _ =>
            Should.ThrowAsync<OperationCanceledException>(
                () => otherInstance.ProcessInboxAsync(otherInstance.PongHooks.Cancellation.Token));

        return otherInstance.Time.GetUtcNow();
    }

    private static async Task<QueueRow> ReadSecretRowAsync(TestModulesHost testModulesHost)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows.Single(row => row.Type == SecretSentType);
    }

    private static async Task<IReadOnlyList<QueueRow>> ReadSecretRowsAsync(
        TestModulesHost testModulesHost,
        String secretPrefix)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows
            .Where(row => row.Type == SecretSentType && row.Payload.Contains(secretPrefix, StringComparison.Ordinal))
            .ToList();
    }

    private static async Task SendSecretToAsync(TestModulesHost testModulesHost, Guid pingId, String secret)
    {
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, secret),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
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
