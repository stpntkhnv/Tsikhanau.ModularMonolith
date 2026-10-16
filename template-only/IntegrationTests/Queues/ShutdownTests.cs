using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class ShutdownTests(PostgresFixture postgresFixture)
{
    private const String PingCreatedType = "pings.ping-created";

    private const String PingSecretSentType = "pings.ping-secret-sent";

    private const String Secret = "s3cret";

    private const String RetryLog = "Message will be retried";

    private const String InsertInboxSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@Id, @Type, @Payload::jsonb, 0, @ReceivedOn, @ReceivedOn, @ReceivedOn)
        """;

    private const String LastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE id = @Id";

    private static readonly TimeSpan BarrierDeadline = TimeSpan.FromSeconds(30);

    private static readonly DateTime LeaseEndsOn =
        (TestModulesHost.TestStartedOn + 2 * TestModulesHost.HandlerTimeout).UtcDateTime;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Host_stop_that_cancels_a_handler_in_a_transaction_gives_the_attempt_back()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(capturingLoggerProvider, stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            handlerStartedSource.TrySetResult();
            return WaitOnTokenLinkedToTheHostStopAsync(testModulesHost, CancellationToken);
        };

        await testModulesHost.CreatePingAsync("stopped");

        await StopWhileTheHandlerRunsAsync(testModulesHost, stopBarrier, handlerStartedSource.Task, PingCreatedType);

        var stoppedOn = testModulesHost.Time.GetUtcNow();
        var row = await ReadRowAsync(testModulesHost, PingCreatedType);
        var lastError = await ReadLastErrorAsync(testModulesHost, row.Id);
        row.Attempts.ShouldBe(0);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe((stoppedOn + TimeSpan.FromMicroseconds(1)).UtcDateTime);
        lastError.ShouldBeNull();
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(RetryLog));
    }

    [Fact]
    public async Task Host_stop_that_cancels_a_handler_outside_a_transaction_gives_the_attempt_back()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(capturingLoggerProvider, stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.WhileHandlingSecret = cancellationToken =>
        {
            handlerStartedSource.TrySetResult();
            return WaitOnTokenLinkedToTheHostStopAsync(testModulesHost, cancellationToken);
        };

        await SendSecretAsync(testModulesHost);

        await StopWhileTheHandlerRunsAsync(testModulesHost, stopBarrier, handlerStartedSource.Task, PingSecretSentType);

        var stoppedOn = testModulesHost.Time.GetUtcNow();
        var row = await ReadRowAsync(testModulesHost, PingSecretSentType);
        var lastError = await ReadLastErrorAsync(testModulesHost, row.Id);
        row.Attempts.ShouldBe(0);
        row.IsProcessed.ShouldBeFalse();
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe((stoppedOn + TimeSpan.FromMicroseconds(1)).UtcDateTime);
        lastError.ShouldBeNull();
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(RetryLog));
    }

    [Fact]
    public async Task Handler_that_ignores_the_host_stop_and_completes_is_processed_once()
    {
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(new CapturingLoggerProvider(), stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            handlerStartedSource.TrySetResult();
            return WaitUntilCancelledAsync(testModulesHost.ApplicationStopping);
        };

        await testModulesHost.CreatePingAsync("finished");

        await StopWhileTheHandlerRunsAsync(testModulesHost, stopBarrier, handlerStartedSource.Task, PingCreatedType);

        var row = await ReadRowAsync(testModulesHost, PingCreatedType);
        var pongCount = await testModulesHost.CountPongsAsync();
        row.IsProcessed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        pongCount.ShouldBe(1);
        testModulesHost.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Failure_a_handler_returns_during_the_host_stop_is_recorded()
    {
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(new CapturingLoggerProvider(), stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.FailFor = ["refused"];
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            handlerStartedSource.TrySetResult();
            return WaitUntilCancelledAsync(testModulesHost.ApplicationStopping);
        };

        await testModulesHost.CreatePingAsync("refused");

        await StopWhileTheHandlerRunsAsync(testModulesHost, stopBarrier, handlerStartedSource.Task, PingCreatedType);

        var row = await ReadRowAsync(testModulesHost, PingCreatedType);
        var lastError = await ReadLastErrorAsync(testModulesHost, row.Id);
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        lastError.ShouldBe("Pong.Rejected: Pong refused this ping");
    }

    [Fact]
    public async Task Secret_in_the_exception_of_a_handler_stopped_through_its_own_token_reaches_no_log()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(capturingLoggerProvider, stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var isSecretThrown = false;
        testModulesHost.PongHooks.WhileHandlingSecret = async cancellationToken =>
        {
            handlerStartedSource.TrySetResult();
            await WaitUntilCancelledAsync(cancellationToken);
            isSecretThrown = true;
            throw new InvalidOperationException(Secret);
        };

        await SendSecretAsync(testModulesHost);
        await handlerStartedSource.Task.WaitAsync(BarrierDeadline, CancellationToken);
        stopBarrier.Release();

        await testModulesHost.StopAsync();

        var row = await ReadRowAsync(testModulesHost, PingSecretSentType);
        isSecretThrown.ShouldBeTrue();
        row.Attempts.ShouldBe(0);
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(Secret));
    }

    [Fact]
    public async Task Secret_in_the_exception_of_a_handler_stopped_by_the_host_reaches_no_log()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(capturingLoggerProvider, stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var isSecretThrown = false;
        testModulesHost.PongHooks.WhileHandlingSecret = async _ =>
        {
            handlerStartedSource.TrySetResult();
            await WaitUntilCancelledAsync(testModulesHost.ApplicationStopping);
            isSecretThrown = true;
            throw new InvalidOperationException(Secret);
        };

        await SendSecretAsync(testModulesHost);

        await StopWhileTheHandlerRunsAsync(testModulesHost, stopBarrier, handlerStartedSource.Task, PingSecretSentType);

        var row = await ReadRowAsync(testModulesHost, PingSecretSentType);
        isSecretThrown.ShouldBeTrue();
        row.Attempts.ShouldBe(0);
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(Secret));
    }

    [Fact]
    public async Task Messages_claimed_before_the_host_stop_are_given_back_without_starting()
    {
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await StartAsync(new CapturingLoggerProvider(), stopBarrier);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            if (Interlocked.Increment(ref startedCount) > 1)
            {
                return Task.CompletedTask;
            }

            handlerStartedSource.TrySetResult();
            return WaitUntilCancelledAsync(testModulesHost.ApplicationStopping);
        };

        await InsertPingCreatedRowAsync(testModulesHost, "first");
        await InsertPingCreatedRowAsync(testModulesHost, "second");
        testModulesHost.Services
            .GetRequiredService<QueueSignals>()
            .Signal(typeof(IPongsModule));

        await handlerStartedSource.Task.WaitAsync(BarrierDeadline, CancellationToken);
        var stopping = testModulesHost.StopAsync();
        try
        {
            await WaitUntilAsync(async () => (await ReadRowsAsync(testModulesHost)).All(row => !IsClaimed(row)));
        }
        finally
        {
            stopBarrier.Release();
        }

        await stopping;

        var rows = await ReadRowsAsync(testModulesHost);
        var processedCount = rows.Count(row => row.IsProcessed);
        processedCount.ShouldBe(1);
        var givenBack = rows.Single(row => !row.IsProcessed);
        givenBack.Attempts.ShouldBe(0);
        givenBack.HasFailed.ShouldBeFalse();
        var started = Volatile.Read(ref startedCount);
        started.ShouldBe(1);
        testModulesHost.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
    }

    private Task<TestModulesHost> StartAsync(
        CapturingLoggerProvider capturingLoggerProvider,
        StopBarrier stopBarrier) =>
        TestModulesHost.StartAsync(
            postgresFixture,
            runInBackground: true,
            configure: builder =>
            {
                builder.Logging
                    .AddProvider(capturingLoggerProvider)
                    .AddFilter<CapturingLoggerProvider>(category: null, LogLevel.Trace);

                builder.Services.AddHostedService(_ => stopBarrier);
            });

    private static async Task StopWhileTheHandlerRunsAsync(
        TestModulesHost testModulesHost,
        StopBarrier stopBarrier,
        Task handlerStarted,
        String type)
    {
        await handlerStarted.WaitAsync(BarrierDeadline, CancellationToken);
        var stopping = testModulesHost.StopAsync();
        try
        {
            await WaitWhileClaimedAsync(testModulesHost, type);
        }
        finally
        {
            stopBarrier.Release();
        }

        await stopping;
    }

    private static async Task WaitWhileClaimedAsync(TestModulesHost testModulesHost, String type)
    {
        var stopwatch = Stopwatch.StartNew();
        while (IsClaimed(await ReadRowAsync(testModulesHost, type)))
        {
            stopwatch.Elapsed.ShouldBeLessThan(BarrierDeadline);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<Boolean>> isDoneAsync)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!await isDoneAsync())
        {
            stopwatch.Elapsed.ShouldBeLessThan(BarrierDeadline);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }

    private static Task<IReadOnlyList<QueueRow>> ReadRowsAsync(TestModulesHost testModulesHost) =>
        testModulesHost.ReadInboxRowsAsync();

    private static Task InsertPingCreatedRowAsync(TestModulesHost testModulesHost, String label)
    {
        var messageId = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(
            new Dictionary<String, String>
            {
                ["id"] = messageId.ToString(),
                ["occurredOn"] = "2026-09-28T08:00:00+00:00",
                ["pingId"] = messageId.ToString(),
                ["label"] = label,
            });

        return testModulesHost.Database.ExecuteAsync(
            InsertInboxSql,
            new
            {
                Id = messageId,
                Type = PingCreatedType,
                Payload = payload,
                ReceivedOn = testModulesHost.Time.GetUtcNow(),
            });
    }

    private static Boolean IsClaimed(QueueRow row) => !row.IsProcessed && row.NextAttemptOn == LeaseEndsOn;

    private static async Task WaitOnTokenLinkedToTheHostStopAsync(
        TestModulesHost testModulesHost,
        CancellationToken cancellationToken)
    {
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            testModulesHost.ApplicationStopping);

        await Task.Delay(Timeout.InfiniteTimeSpan, linkedSource.Token);
    }

    private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        var cancelledSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.Register(cancelledSource.SetResult);
        await cancelledSource.Task;
    }

    private static async Task<QueueRow> ReadRowAsync(TestModulesHost testModulesHost, String type)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows.Single(row => row.Type == type);
    }

    private static Task<String?> ReadLastErrorAsync(TestModulesHost testModulesHost, Guid messageId) =>
        testModulesHost.Database.ExecuteScalarAsync<String?>(LastErrorSql, new { Id = messageId });

    private static async Task SendSecretAsync(TestModulesHost testModulesHost)
    {
        var pingId = await testModulesHost.CreatePingAsync("secret holder");
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, Secret),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}
