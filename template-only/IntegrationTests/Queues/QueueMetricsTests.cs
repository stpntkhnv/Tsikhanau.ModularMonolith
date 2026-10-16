using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class QueueMetricsTests(PostgresFixture postgresFixture)
{
    private const String MessagesInstrument = "tsikhanau.queue.messages";

    private const String HandlingDurationInstrument = "tsikhanau.queue.handling.duration";

    private const String PendingInstrument = "tsikhanau.queue.pending";

    private const String FailedInstrument = "tsikhanau.queue.failed";

    private const String OldestDueAgeInstrument = "tsikhanau.queue.oldest_due.age";

    private const String ModuleTag = "module";

    private const String QueueTag = "queue";

    private const String OutcomeTag = "outcome";

    private const String Pings = "Pings";

    private const String Pongs = "Pongs";

    private const String Outbox = "outbox_messages";

    private const String Inbox = "inbox_messages";

    private const String InternalCommands = "internal_commands";

    private const String Processed = "processed";

    private const String Retried = "retried";

    private const String Failed = "failed";

    private const String GivenBack = "given_back";

    private const Int32 MaxAttempts = 7;

    private const String TestSourceName = "Tsikhanau.Monolith.IntegrationTests";

    private const String PingCreatedSpanName = "IPongsModule inbox_messages pings.ping-created";

    private const String InsertPingCreatedSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@Id, 'pings.ping-created', @Payload::jsonb, 0, @ReceivedOn, @ReceivedOn, @ReceivedOn)
        """;

    private const String InsertPongsInboxRowSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on, failed_on)
        VALUES (@Id, 'seeded', '{}'::jsonb, @Attempts, @NextAttemptOn, @NextAttemptOn, @NextAttemptOn, @FailedOn)
        """;

    private const String InsertPingsOutboxRowSql = """
        INSERT INTO pings.outbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, failed_on)
        VALUES (@Id, 'seeded', '{}'::jsonb, @Attempts, @NextAttemptOn, @NextAttemptOn, @FailedOn)
        """;

    private const String InsertPingsInternalCommandRowSql = """
        INSERT INTO pings.internal_commands (id, type, payload, attempts, next_attempt_on, enqueued_on, failed_on)
        VALUES (@Id, 'seeded', '{}'::jsonb, @Attempts, @NextAttemptOn, @NextAttemptOn, @FailedOn)
        """;

    private static readonly (String Module, String Queue)[] EveryQueue =
    [
        (Pings, Inbox),
        (Pings, InternalCommands),
        (Pings, Outbox),
        (Pongs, Inbox),
        (Pongs, InternalCommands),
        (Pongs, Outbox),
    ];

    private static readonly TimeSpan BarrierDeadline = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private static readonly DateTimeOffset LeaseEndsOn =
        TestModulesHost.TestStartedOn + 2 * TestModulesHost.HandlerTimeout;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Message_handled_in_a_transaction_is_counted_processed_once_with_its_module_and_queue()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        await testModulesHost.CreatePingAsync("counted");

        await testModulesHost.ProcessQueuesAsync();

        var messageCounts = ReadMessageCounts(metricRecorder);
        var tagNames = metricRecorder
            .ReadRecorded(MessagesInstrument)
            .Select(measurement => String.Join(",", measurement.Tags.Keys.Order(StringComparer.Ordinal)))
            .Distinct()
            .ToList();

        messageCounts.ShouldBe([(Pings, Outbox, Processed, 1), (Pongs, Inbox, Processed, 1)], ignoreOrder: true);
        tagNames.ShouldBe(["module,outcome,queue"]);
    }

    [Fact]
    public async Task Message_handled_outside_a_transaction_is_counted_processed_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await CreateProcessedPingAsync(testModulesHost);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        await SendSecretAsync(testModulesHost, pingId);

        await testModulesHost.ProcessQueuesAsync();

        var messageCounts = ReadMessageCounts(metricRecorder);

        messageCounts.ShouldBe([(Pings, Outbox, Processed, 1), (Pongs, Inbox, Processed, 1)], ignoreOrder: true);
    }

    [Fact]
    public async Task Message_whose_handler_throws_is_counted_retried_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        await testModulesHost.CreatePingAsync("flaky");

        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var messageCounts = ReadMessageCounts(metricRecorder);
        var handlingSeconds = ReadHandlingSeconds(metricRecorder, Pongs, Inbox);

        messageCounts.ShouldBe([(Pings, Outbox, Processed, 1), (Pongs, Inbox, Retried, 1)], ignoreOrder: true);
        handlingSeconds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Message_with_a_permanent_error_is_counted_failed_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("broken");

        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var messageCounts = ReadMessageCounts(metricRecorder);

        messageCounts.ShouldBe([(Pings, Outbox, Processed, 1), (Pongs, Inbox, Failed, 1)], ignoreOrder: true);
    }

    [Fact]
    public async Task Message_whose_attempts_ran_out_before_its_handler_is_counted_failed_without_a_handling_time()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("crashed");
        await testModulesHost.DeliverOutboxAsync();
        await testModulesHost.Database.ExecuteAsync(
            "UPDATE pongs.inbox_messages SET attempts = @Attempts",
            new { Attempts = MaxAttempts });

        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);
        var handlingSeconds = ReadHandlingSeconds(metricRecorder, Pongs, Inbox);

        messageCounts.ShouldBe([(Failed, 1)]);
        handlingSeconds.ShouldBeEmpty();
        testModulesHost.PongHooks.ReceivedPingIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Batch_that_goes_back_unstarted_is_counted_given_back_with_its_size()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await CreatePingsAsync(testModulesHost, "unstarted", 3);
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(6));
            return Task.CompletedTask;
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);

        messageCounts.ShouldBe([(Processed, 1), (GivenBack, 2)]);
    }

    [Fact]
    public async Task Messages_the_host_stop_gives_back_are_counted_given_back()
    {
        var stopBarrier = new StopBarrier();
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            runInBackground: true,
            configure: builder => builder.Services.AddHostedService(_ => stopBarrier));

        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            if (Interlocked.Increment(ref startedCount) > 1)
            {
                return Task.CompletedTask;
            }

            handlerStartedSource.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, testModulesHost.ApplicationStopping);
        };

        await InsertPingCreatedRowAsync(testModulesHost, "in flight");
        await InsertPingCreatedRowAsync(testModulesHost, "buffered");
        testModulesHost.Services
            .GetRequiredService<QueueSignals>()
            .Signal(typeof(IPongsModule));

        await handlerStartedSource.Task.WaitAsync(BarrierDeadline, CancellationToken);
        var stopping = testModulesHost.StopAsync();
        try
        {
            await WaitUntilAsync(async () => (await testModulesHost.ReadInboxRowsAsync()).All(row => !IsClaimed(row)));
        }
        finally
        {
            stopBarrier.Release();
        }

        await stopping;

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);
        var started = Volatile.Read(ref startedCount);

        messageCounts.ShouldBe([(GivenBack, 1), (GivenBack, 1)]);
        started.ShouldBe(1);
    }

    [Fact]
    public async Task Batch_given_back_counts_only_the_rows_its_update_touched()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        await CreatePingsAsync(testModulesHost, "partly-taken", 3);
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        IReadOnlyList<InboxMessage> claimedByOther = [];
        testModulesHost.PongHooks.BeforeHandle = async () =>
        {
            testModulesHost.PongHooks.BeforeHandle = static () => Task.CompletedTask;
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(6));
            claimedByOther = await ClaimInOtherInstanceAsync(otherInstance);
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);

        claimedByOther.Count.ShouldBe(1);
        messageCounts.ShouldBe([(Processed, 1), (GivenBack, 1)]);
    }

    [Fact]
    public async Task Message_another_instance_took_before_the_lock_counts_nothing()
    {
        await using var staleInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        await CreatePingsAsync(staleInstance, "taken-before-the-lock", 2);
        await staleInstance.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(staleInstance.Services);
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

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);
        var handlingSeconds = ReadHandlingSeconds(metricRecorder, Pongs, Inbox);

        staleInstance.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
        messageCounts.ShouldBe([(Processed, 1)]);
        handlingSeconds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Message_another_instance_took_before_its_mark_counts_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        var pingId = await CreateProcessedPingAsync(testModulesHost);
        await SendSecretAsync(testModulesHost, pingId);
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        IReadOnlyList<InboxMessage> claimedByOther = [];
        testModulesHost.PongHooks.WhileHandlingSecret = async _ =>
            claimedByOther = await ClaimInOtherInstanceAsync(otherInstance);

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);
        var handlingSeconds = ReadHandlingSeconds(metricRecorder, Pongs, Inbox);

        claimedByOther
            .ShouldHaveSingleItem().Type
            .ShouldBe("pings.ping-secret-sent");

        messageCounts.ShouldBeEmpty();
        handlingSeconds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Failure_of_a_message_another_instance_took_counts_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var otherInstance = await TestModulesHost.StartAsync(postgresFixture);
        otherInstance.Time.Advance(2 * TestModulesHost.HandlerTimeout);
        var pingId = await CreateProcessedPingAsync(testModulesHost);
        await SendSecretAsync(testModulesHost, pingId);
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        IReadOnlyList<InboxMessage> claimedByOther = [];
        testModulesHost.PongHooks.WhileHandlingSecret = async _ =>
        {
            claimedByOther = await ClaimInOtherInstanceAsync(otherInstance);
            throw new InvalidOperationException("handler failed after another instance took the message");
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);
        var handlingSeconds = ReadHandlingSeconds(metricRecorder, Pongs, Inbox);

        claimedByOther
            .ShouldHaveSingleItem().Type
            .ShouldBe("pings.ping-secret-sent");

        messageCounts.ShouldBeEmpty();
        handlingSeconds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Give_back_that_fails_with_a_database_error_counts_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var giveBackRefusal = await GiveBackRefusal.InstallAsync(
            testModulesHost.Database,
            PongsDbContext.SchemaName,
            InboxMessage.Table,
            "P0001");

        testModulesHost.PongHooks.CancelFor = ["cancelled"];
        await testModulesHost.CreatePingAsync("cancelled");
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);

        await Should.ThrowAsync<OperationCanceledException>(
            () => testModulesHost.ProcessInboxAsync(testModulesHost.PongHooks.Cancellation.Token));

        var row = (await testModulesHost.ReadInboxRowsAsync()).Single();
        var messageCounts = ReadMessageCounts(metricRecorder, Pongs, Inbox);

        row.Attempts.ShouldBe(1);
        messageCounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handling_time_is_measured_in_seconds_once_for_each_handler_that_ran()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.ThrowFor = ["flaky"];
        testModulesHost.PongHooks.FailFor = ["broken"];
        await testModulesHost.CreatePingAsync("fine");
        await testModulesHost.CreatePingAsync("flaky");
        await testModulesHost.CreatePingAsync("broken");
        await testModulesHost.DeliverOutboxAsync();
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            testModulesHost.Time.Advance(TimeSpan.FromMilliseconds(250));
            return Task.CompletedTask;
        };

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var handlingMeasurements = metricRecorder.ReadRecorded(HandlingDurationInstrument);
        var handlingSeconds = handlingMeasurements
            .Select(measurement => measurement.Value)
            .ToList();

        var handlingTags = handlingMeasurements
            .Select(measurement => (measurement.Tags.Count, measurement.Tags[ModuleTag], measurement.Tags[QueueTag]))
            .Distinct()
            .ToList();

        handlingSeconds.ShouldBe([0.25, 0.25, 0.25]);
        handlingTags.ShouldBe([(2, Pongs, Inbox)]);
    }

    [Fact]
    public async Task Span_of_a_failed_message_has_the_error_status_with_the_failure_code()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name is AppDiagnostics.SourceName or TestSourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Enqueue,
        };

        ActivitySource.AddActivityListener(activityListener);
        using var testActivitySource = new ActivitySource(TestSourceName);
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["broken"];
        ActivityTraceId traceId;
        using (var rootActivity = testActivitySource.StartActivity("root")!)
        {
            traceId = rootActivity.TraceId;
            await testModulesHost.CreatePingAsync("broken");
            await testModulesHost.CreatePingAsync("fine");
        }

        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var inboxSpanStatuses = stoppedActivities
            .Where(activity => activity.TraceId == traceId && activity.DisplayName == PingCreatedSpanName)
            .Select(activity => (activity.Status, activity.StatusDescription ?? String.Empty))
            .ToList();

        inboxSpanStatuses.ShouldBe(
            [(ActivityStatusCode.Error, "Pong.Rejected"), (ActivityStatusCode.Unset, String.Empty)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Sample_reports_pending_failed_and_the_oldest_due_age_of_every_module_queue()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        var sampledOn = testModulesHost.Time.GetUtcNow();
        await InsertPendingRowAsync(testModulesHost, InsertPongsInboxRowSql, 0, sampledOn - TimeSpan.FromSeconds(30));
        await InsertPendingRowAsync(testModulesHost, InsertPongsInboxRowSql, 0, sampledOn - TimeSpan.FromSeconds(10));
        await InsertPendingRowAsync(testModulesHost, InsertPongsInboxRowSql, 1, LeaseEndsOn);
        await InsertFailedRowAsync(testModulesHost, InsertPongsInboxRowSql);
        await InsertFailedRowAsync(testModulesHost, InsertPongsInboxRowSql);
        await InsertPendingRowAsync(testModulesHost, InsertPingsOutboxRowSql, 1, sampledOn + TimeSpan.FromMinutes(1));
        await InsertFailedRowAsync(testModulesHost, InsertPingsInternalCommandRowSql);
        var pendingCountsBeforeASample = ObserveGauge(metricRecorder, PendingInstrument);

        await testModulesHost.SampleQueuesAsync();

        var pendingCounts = ObserveGauge(metricRecorder, PendingInstrument);
        var failedCounts = ObserveGauge(metricRecorder, FailedInstrument);
        var oldestDueAges = ObserveGauge(metricRecorder, OldestDueAgeInstrument);
        testModulesHost.Time.Advance(TimeSpan.FromSeconds(20));
        await testModulesHost.SampleQueuesAsync();
        var laterOldestDueAges = ObserveGauge(metricRecorder, OldestDueAgeInstrument);

        pendingCountsBeforeASample.ShouldBeEmpty();
        pendingCounts.ShouldBe(ForEveryQueue((Pings, Outbox, 1), (Pongs, Inbox, 3)), ignoreOrder: true);
        failedCounts.ShouldBe(ForEveryQueue((Pings, InternalCommands, 1), (Pongs, Inbox, 2)), ignoreOrder: true);
        oldestDueAges.ShouldBe(ForEveryQueue((Pongs, Inbox, 30)), ignoreOrder: true);
        laterOldestDueAges.ShouldBe(ForEveryQueue((Pongs, Inbox, 50)), ignoreOrder: true);
    }

    [Fact]
    public async Task Sampler_refreshes_its_values_every_minute_while_a_handler_holds_the_pump()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, runInBackground: true);
        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        await WaitUntilAsync(() => Task.FromResult(HasSampledEveryModule(metricRecorder)));
        var handlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            handlerStartedSource.TrySetResult();
            return releaseSource.Task;
        };

        try
        {
            await testModulesHost.CreatePingAsync("blocked");
            await handlerStartedSource.Task.WaitAsync(BarrierDeadline, CancellationToken);
            await InsertFailedRowAsync(testModulesHost, InsertPongsInboxRowSql);
            await InsertFailedRowAsync(testModulesHost, InsertPingsOutboxRowSql);

            await AdvanceUntilAsync(
                testModulesHost,
                SampleInterval,
                () => IsObserved(metricRecorder, FailedInstrument, Pongs, Inbox, 1)
                    && IsObserved(metricRecorder, FailedInstrument, Pings, Outbox, 1));

            var inboxRow = (await testModulesHost.ReadInboxRowsAsync()).Single(row => row.Type != "seeded");
            var isPendingInboxRowCounted = IsObserved(metricRecorder, PendingInstrument, Pongs, Inbox, 1);

            inboxRow.IsProcessed.ShouldBeFalse();
            inboxRow.Attempts.ShouldBe(1);
            isPendingInboxRowCounted.ShouldBeTrue();
            testModulesHost.PongHooks.ReceivedPingIds.Count.ShouldBe(1);
        }
        finally
        {
            releaseSource.TrySetResult();
        }
    }

    [Fact]
    public async Task Sample_of_a_module_without_a_free_connection_ends_at_its_timeout_and_keeps_its_values()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            configure: builder => builder.Logging.AddProvider(capturingLoggerProvider));

        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        await testModulesHost.SampleQueuesAsync();
        var heldConnections = await HoldEveryConnectionAsync(testModulesHost, typeof(IPongsModule));
        try
        {
            await InsertFailedRowAsync(testModulesHost, InsertPingsOutboxRowSql);
            await InsertFailedRowAsync(testModulesHost, InsertPongsInboxRowSql);
            capturingLoggerProvider.Clear();

            var sampling = testModulesHost.SampleQueuesAsync();
            await WaitUntilAsync(() => Task.FromResult(IsObserved(metricRecorder, FailedInstrument, Pings, Outbox, 1)));
            var hasEndedBeforeItsTimeout = sampling.IsCompleted;
            testModulesHost.Time.Advance(SampleTimeout);
            await sampling.WaitAsync(SampleTimeout, CancellationToken);

            var failedCounts = ObserveGauge(metricRecorder, FailedInstrument);
            var warnings = capturingLoggerProvider.Entries
                .Where(entry => entry.Level >= LogLevel.Warning)
                .Select(entry => entry.Message)
                .ToList();

            hasEndedBeforeItsTimeout.ShouldBeFalse();
            failedCounts.ShouldBe(ForEveryQueue((Pings, Outbox, 1)), ignoreOrder: true);
            warnings
                .ShouldHaveSingleItem()
                .ShouldStartWith("Pongs queue sample");
        }
        finally
        {
            await DisposeAllAsync(heldConnections);
        }
    }

    [Fact]
    public async Task Two_hosts_in_one_process_never_see_each_others_measurements()
    {
        await using var firstHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var secondHost = await TestModulesHost.StartAsync(postgresFixture);
        using var firstMetricRecorder = MetricRecorder.StartFor(firstHost.Services);
        using var secondMetricRecorder = MetricRecorder.StartFor(secondHost.Services);
        await firstHost.CreatePingAsync("first host only");

        await firstHost.ProcessQueuesAsync();
        await firstHost.SampleQueuesAsync();

        var firstMessageCounts = ReadMessageCounts(firstMetricRecorder);
        var firstPendingCounts = ObserveGauge(firstMetricRecorder, PendingInstrument);
        var secondRecorded = secondMetricRecorder.Recorded;
        var secondPendingCounts = ObserveGauge(secondMetricRecorder, PendingInstrument);

        firstMessageCounts.ShouldBe([(Pings, Outbox, Processed, 1), (Pongs, Inbox, Processed, 1)], ignoreOrder: true);
        firstPendingCounts.Count.ShouldBe(6);
        secondRecorded.ShouldBeEmpty();
        secondPendingCounts.ShouldBeEmpty();
    }

    private static List<(String Module, String Queue, String Outcome, Double Count)> ReadMessageCounts(
        MetricRecorder metricRecorder) =>
        metricRecorder
            .ReadRecorded(MessagesInstrument)
            .Select(measurement => (
                measurement.Tags[ModuleTag],
                measurement.Tags[QueueTag],
                measurement.Tags[OutcomeTag],
                measurement.Value))
            .ToList();

    private static List<(String Outcome, Double Count)> ReadMessageCounts(
        MetricRecorder metricRecorder,
        String module,
        String queue) =>
        metricRecorder
            .ReadRecorded(MessagesInstrument)
            .Where(measurement => IsOf(measurement, module, queue))
            .Select(measurement => (measurement.Tags[OutcomeTag], measurement.Value))
            .ToList();

    private static List<Double> ReadHandlingSeconds(MetricRecorder metricRecorder, String module, String queue) =>
        metricRecorder
            .ReadRecorded(HandlingDurationInstrument)
            .Where(measurement => IsOf(measurement, module, queue))
            .Select(measurement => measurement.Value)
            .ToList();

    private static List<(String Module, String Queue, Double Value)> ObserveGauge(
        MetricRecorder metricRecorder,
        String instrumentName) =>
        metricRecorder
            .Observe(instrumentName)
            .Select(measurement => (measurement.Tags[ModuleTag], measurement.Tags[QueueTag], measurement.Value))
            .ToList();

    private static Boolean IsObserved(
        MetricRecorder metricRecorder,
        String instrumentName,
        String module,
        String queue,
        Double value) =>
        ObserveGauge(metricRecorder, instrumentName)
            .Contains((module, queue, value));

    private static Boolean HasSampledEveryModule(MetricRecorder metricRecorder) =>
        ObserveGauge(metricRecorder, PendingInstrument).Count == 6;

    private static Boolean IsOf(RecordedMeasurement measurement, String module, String queue) =>
        measurement.Tags[ModuleTag] == module && measurement.Tags[QueueTag] == queue;

    private static List<(String Module, String Queue, Double Value)> ForEveryQueue(
        params (String Module, String Queue, Double Value)[] nonZeroValues) =>
        EveryQueue
            .Select(moduleQueue => (moduleQueue.Module, moduleQueue.Queue, FindValue(nonZeroValues, moduleQueue)))
            .ToList();

    private static Double FindValue(
        IEnumerable<(String Module, String Queue, Double Value)> values,
        (String Module, String Queue) moduleQueue) =>
        values
            .Where(value => value.Module == moduleQueue.Module && value.Queue == moduleQueue.Queue)
            .Select(value => value.Value)
            .FirstOrDefault();

    private static Task<IReadOnlyList<InboxMessage>> ClaimInOtherInstanceAsync(TestModulesHost otherInstance) =>
        otherInstance.Services
            .GetRequiredService<InboxPump<IPongsModule>>()
            .ClaimBatchAsync(TestModulesHost.ClaimBatchSize, CancellationToken);

    private static async Task CreatePingsAsync(TestModulesHost testModulesHost, String labelPrefix, Int32 count)
    {
        for (var pingNumber = 0; pingNumber < count; pingNumber++)
        {
            await testModulesHost.CreatePingAsync($"{labelPrefix}-{pingNumber}");
        }
    }

    private static async Task<Guid> CreateProcessedPingAsync(TestModulesHost testModulesHost)
    {
        var pingId = await testModulesHost.CreatePingAsync("secret holder");
        await testModulesHost.ProcessQueuesAsync();
        return pingId;
    }

    private static async Task SendSecretAsync(TestModulesHost testModulesHost, Guid pingId)
    {
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, "s3cret"),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

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
            InsertPingCreatedSql,
            new { Id = messageId, Payload = payload, ReceivedOn = testModulesHost.Time.GetUtcNow() });
    }

    private static Task InsertPendingRowAsync(
        TestModulesHost testModulesHost,
        String insertSql,
        Int32 attempts,
        DateTimeOffset nextAttemptOn) =>
        testModulesHost.Database.ExecuteAsync(
            insertSql,
            new
            {
                Id = Guid.CreateVersion7(),
                Attempts = attempts,
                NextAttemptOn = nextAttemptOn,
                FailedOn = (DateTimeOffset?)null,
            });

    private static Task InsertFailedRowAsync(TestModulesHost testModulesHost, String insertSql)
    {
        var failedOn = testModulesHost.Time.GetUtcNow() - TimeSpan.FromHours(1);
        return testModulesHost.Database.ExecuteAsync(
            insertSql,
            new { Id = Guid.CreateVersion7(), Attempts = MaxAttempts, NextAttemptOn = failedOn, FailedOn = failedOn });
    }

    private static async Task<List<NpgsqlConnection>> HoldEveryConnectionAsync(
        TestModulesHost testModulesHost,
        Type moduleKey)
    {
        var npgsqlDataSource = testModulesHost.Services.GetRequiredKeyedService<NpgsqlDataSource>(moduleKey);
        var maxPoolSize = new NpgsqlConnectionStringBuilder(npgsqlDataSource.ConnectionString).MaxPoolSize;
        var connections = new List<NpgsqlConnection>();
        for (var connectionNumber = 0; connectionNumber < maxPoolSize; connectionNumber++)
        {
            connections.Add(await npgsqlDataSource.OpenConnectionAsync(CancellationToken));
        }

        return connections;
    }

    private static async Task DisposeAllAsync(IEnumerable<NpgsqlConnection> connections)
    {
        foreach (var connection in connections)
        {
            await connection.DisposeAsync();
        }
    }

    private static Boolean IsClaimed(QueueRow row) =>
        !row.IsProcessed && row.NextAttemptOn == LeaseEndsOn.UtcDateTime;

    private static async Task WaitUntilAsync(Func<Task<Boolean>> isDoneAsync)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!await isDoneAsync())
        {
            stopwatch.Elapsed.ShouldBeLessThan(BarrierDeadline);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }

    private static async Task AdvanceUntilAsync(TestModulesHost testModulesHost, TimeSpan step, Func<Boolean> isDone)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!isDone())
        {
            stopwatch.Elapsed.ShouldBeLessThan(BarrierDeadline);
            testModulesHost.Time.Advance(step);
            await WaitBrieflyUntilAsync(isDone);
        }
    }

    private static async Task WaitBrieflyUntilAsync(Func<Boolean> isDone)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!isDone() && stopwatch.Elapsed < SampleTimeout)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }
}
