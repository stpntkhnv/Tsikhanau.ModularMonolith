using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class ShutdownProcessTests(PostgresFixture postgresFixture)
{
    private const String UnixOnly = "the test sends Unix signals to the test host";

    private const String UrlsKey = TestHostEnvironment.UrlsKey;

    private const String PollIntervalKey = "App__Queues__PollInterval";

    private const String CleanupInBackgroundKey = "App__Cleanup__RunInBackground";

    private const String BlockAfterPongWriteKey = "TestHost__BlockAfterPongWrite";

    private const String BlockUntilStoppingKey = "TestHost__BlockUntilStopping";

    private const String SecretEffectPathKey = "TestHost__SecretEffectPath";

    private const String BlockSecretHandlerKey = "TestHost__BlockSecretHandler";

    private const String HoldStopUntilClassifiedKey = "TestHost__HoldStopUntilClassified";

    private const String ReportPongsPassesKey = "TestHost__ReportPongsPasses";

    private const String PongHandlerBlocks = "probe: pong written, its handler blocks";

    private const String PongHandlerEnds = "probe: pong handler ends";

    private const String SecretHandlerBlocks = "probe: secret handler blocks";

    private const String SecretHandlerEnds = "probe: secret handler ends";

    private const String StopBarrierReleased = "probe: stop barrier released on";

    private const String PassHandledOne = "probe: Pongs pass handled 1\n";

    private const String PassHandledNone = "probe: Pongs pass handled 0\n";

    private const String AbandonedAtShutdown = "IPongsModule handler abandoned at shutdown";

    private const String PingsAbandonedAtShutdown = "IPingsModule handler abandoned at shutdown";

    private const String TransactionError = "An error occurred using a transaction";

    private const String ErrorLine = "fail: ";

    private const String Secret = "s3cret";

    private const Int32 KilledExitCode = 137;

    private const String InboxRowSql = """
        SELECT attempts AS "Attempts", next_attempt_on AS "NextAttemptOn", received_on AS "ReceivedOn",
               processed_on AS "ProcessedOn", failed_on AS "FailedOn", last_error AS "LastError"
        FROM pongs.inbox_messages
        """;

    private const String PongCountSql = "SELECT count(*) FROM pongs.pongs";

    private const String InsertSecretRowSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@Id, 'pings.ping-secret-sent', @Payload::jsonb, 0, now(), now(), now())
        """;

    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(25);

    private static readonly TimeSpan PastTheShutdownTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan IdleExitLimit = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan LongHandlerTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan ShortHandlerTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan KilledHandlerTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan HandlerStartTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan PassTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ClaimDelayLimit = TimeSpan.FromSeconds(5);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sigterm_waits_for_a_handler_that_ignores_it_and_commits_its_finished_work()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[BlockAfterPongWriteKey] = FormatDuration(TimeSpan.FromSeconds(20));

        using var testHost = TestHostProcess.Start(environment);
        var handlerBlockedEnd = await BlockAPongHandlerAsync(testHost, environment[UrlsKey]);
        await testHost.TerminateAsync();
        var exit = await testHost.WaitForExitAsync();
        var outputAfterTheBlock = exit.Output[handlerBlockedEnd..];
        var stoppedRow = await ReadInboxRowAsync(connectionString);
        var pongCount = await CountPongsAsync(connectionString);

        exit.Code.ShouldBe(0, exit.Text);
        exit.SinceSignal.ShouldBeLessThan(ShutdownTimeout);
        exit.Output.ShouldContain(PongHandlerEnds);
        outputAfterTheBlock.ShouldNotContain(TransactionError);
        outputAfterTheBlock.ShouldNotContain(ErrorLine);
        exit.Output.ShouldNotContain(AbandonedAtShutdown);
        ShouldBeProcessed(stoppedRow);
        pongCount.ShouldBe(1);
        await ShouldBeUnchangedByAnotherProcessAsync(connectionString, stoppedRow);
    }

    [Fact]
    public async Task Handler_running_past_the_shutdown_timeout_is_abandoned_and_returns_after_its_lease()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var environment = CreateEnvironment(connectionString, ShortHandlerTimeout);
        environment[BlockAfterPongWriteKey] = FormatDuration(TimeSpan.FromSeconds(40));

        using var testHost = TestHostProcess.Start(environment);
        await BlockAPongHandlerAsync(testHost, environment[UrlsKey]);
        await testHost.TerminateAsync();
        var exit = await testHost.WaitForExitAsync();
        var stoppedRow = await ReadInboxRowAsync(connectionString);
        var pongCount = await CountPongsAsync(connectionString);

        exit.Code.ShouldBe(0, exit.Text);
        exit.SinceSignal.ShouldBeInRange(ShutdownTimeout, PastTheShutdownTimeout);
        exit.Output.ShouldContain(AbandonedAtShutdown);
        exit.Output.ShouldNotContain(PingsAbandonedAtShutdown);
        exit.Output.ShouldNotContain(PongHandlerEnds);
        ShouldBeLeased(stoppedRow, ShortHandlerTimeout);
        pongCount.ShouldBe(0);
        await RecoverAsync(connectionString, stoppedRow, () => CountPongsAsync(connectionString));
    }

    [Fact]
    public async Task Sigkill_after_the_grace_period_rolls_back_and_the_message_returns_after_its_lease()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var environment = CreateEnvironment(connectionString, KilledHandlerTimeout);
        environment[BlockAfterPongWriteKey] = FormatDuration(TimeSpan.FromSeconds(20));

        using var testHost = TestHostProcess.Start(environment);
        await BlockAPongHandlerAsync(testHost, environment[UrlsKey]);
        await testHost.TerminateAsync(GracePeriod);
        var exit = await testHost.WaitForExitAsync();
        var stoppedRow = await ReadInboxRowAsync(connectionString);
        var pongCount = await CountPongsAsync(connectionString);

        exit.Code.ShouldBe(KilledExitCode, exit.Text);
        exit.SinceSignal.ShouldBeGreaterThanOrEqualTo(GracePeriod);
        exit.Output.ShouldNotContain(PongHandlerEnds);
        ShouldBeLeased(stoppedRow, KilledHandlerTimeout);
        pongCount.ShouldBe(0);
        await RecoverAsync(connectionString, stoppedRow, () => CountPongsAsync(connectionString));
    }

    [Fact]
    public async Task Handler_cancelled_by_application_stopping_gives_its_attempt_back()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[BlockAfterPongWriteKey] = FormatDuration(TimeSpan.FromMinutes(1));
        environment[BlockUntilStoppingKey] = "true";
        environment[HoldStopUntilClassifiedKey] = "true";

        using var testHost = TestHostProcess.Start(environment);
        await BlockAPongHandlerAsync(testHost, environment[UrlsKey]);
        await testHost.TerminateAsync();
        var exit = await testHost.WaitForExitAsync();
        var stoppedRow = await ReadInboxRowAsync(connectionString);
        var pongCount = await CountPongsAsync(connectionString);

        exit.Code.ShouldBe(0, exit.Text);
        exit.SinceSignal.ShouldBeLessThan(ShutdownTimeout);
        exit.Output.ShouldContain(PongHandlerEnds);
        exit.Output.ShouldContain($"{StopBarrierReleased} given_back");
        ShouldBeGivenBack(stoppedRow, LongHandlerTimeout);
        pongCount.ShouldBe(0);
        await RecoverAsync(connectionString, stoppedRow, () => CountPongsAsync(connectionString));
    }

    [Fact]
    public async Task Handler_outside_a_transaction_cancelled_by_its_token_is_given_back_before_its_effect()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var effectPath = Path.Combine(Path.GetTempPath(), $"secret-effect-{Guid.CreateVersion7():N}");
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[SecretEffectPathKey] = effectPath;
        environment[BlockSecretHandlerKey] = FormatDuration(TimeSpan.FromMinutes(1));
        try
        {
            using var testHost = TestHostProcess.Start(environment);
            await testHost.WaitUntilHealthyAsync(environment[UrlsKey]);
            await InsertSecretRowAsync(connectionString);
            await testHost.WaitForOutputAsync(SecretHandlerBlocks, HandlerStartTimeout);
            await testHost.TerminateAsync();
            var exit = await testHost.WaitForExitAsync();
            var stoppedRow = await ReadInboxRowAsync(connectionString);
            var effectCount = await CountEffectsAsync(effectPath);

            exit.Code.ShouldBe(0, exit.Text);
            exit.SinceSignal.ShouldBeLessThan(ShutdownTimeout);
            exit.Output.ShouldContain(SecretHandlerEnds);
            ShouldBeGivenBack(stoppedRow, LongHandlerTimeout);
            effectCount.ShouldBe(0);
            await RecoverAsync(
                connectionString,
                stoppedRow,
                () => CountEffectsAsync(effectPath),
                new Dictionary<String, String> { [SecretEffectPathKey] = effectPath });
        }
        finally
        {
            File.Delete(effectPath);
        }
    }

    [Fact]
    public async Task Idle_host_exits_within_five_seconds_of_sigterm()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreateDatabaseAsync();
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[PollIntervalKey] = FormatDuration(TimeSpan.FromMinutes(1));
        environment[CleanupInBackgroundKey] = "true";

        using var testHost = TestHostProcess.Start(environment);
        await testHost.WaitUntilHealthyAsync(environment[UrlsKey]);
        await testHost.TerminateAsync();
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldBe(0, exit.Text);
        exit.SinceSignal.ShouldBeLessThan(IdleExitLimit);
    }

    private static async Task<Int32> BlockAPongHandlerAsync(TestHostProcess testHost, String url)
    {
        await testHost.WaitUntilHealthyAsync(url);
        using var httpClient = new HttpClient();
        using var content = new StringContent("""{"label":"stopped"}""", Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync($"{url}/pings", content, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await testHost.WaitForOutputAsync(PongHandlerBlocks, HandlerStartTimeout);
    }

    private static Task RecoverAsync(
        String connectionString,
        InboxRow stoppedRow,
        Func<Task<Int64>> countEffectsAsync) =>
        RecoverAsync(connectionString, stoppedRow, countEffectsAsync, new Dictionary<String, String>());

    private static async Task RecoverAsync(
        String connectionString,
        InboxRow stoppedRow,
        Func<Task<Int64>> countEffectsAsync,
        Dictionary<String, String> probes)
    {
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[ReportPongsPassesKey] = "true";
        foreach (var (key, value) in probes)
        {
            environment[key] = value;
        }

        using var testHost = TestHostProcess.Start(environment);
        var handledEnd = await testHost.WaitForOutputAsync(PassHandledOne, RecoveryTimeout);
        var recoveredRow = await ReadInboxRowAsync(connectionString);
        var effectCount = await countEffectsAsync();
        await testHost.WaitForOutputAsync(PassHandledNone, PassTimeout, handledEnd);
        var rowAfterAnotherPass = await ReadInboxRowAsync(connectionString);
        var effectCountAfterAnotherPass = await countEffectsAsync();

        recoveredRow.Attempts.ShouldBe(stoppedRow.Attempts + 1);
        recoveredRow.ProcessedOn
            .ShouldNotBeNull()
            .ShouldBeGreaterThanOrEqualTo(stoppedRow.NextAttemptOn);

        recoveredRow.FailedOn.ShouldBeNull();
        recoveredRow.LastError.ShouldBeNull();
        effectCount.ShouldBe(1);
        rowAfterAnotherPass.ShouldBe(recoveredRow);
        effectCountAfterAnotherPass.ShouldBe(1);
    }

    private static async Task ShouldBeUnchangedByAnotherProcessAsync(String connectionString, InboxRow stoppedRow)
    {
        var environment = CreateEnvironment(connectionString, LongHandlerTimeout);
        environment[ReportPongsPassesKey] = "true";

        using var testHost = TestHostProcess.Start(environment);
        await testHost.WaitForOutputAsync(PassHandledNone, RecoveryTimeout);
        var rowAfterAPass = await ReadInboxRowAsync(connectionString);
        var pongCountAfterAPass = await CountPongsAsync(connectionString);

        rowAfterAPass.ShouldBe(stoppedRow);
        pongCountAfterAPass.ShouldBe(1);
    }

    private static void ShouldBeProcessed(InboxRow row)
    {
        row.Attempts.ShouldBe(1);
        row.ProcessedOn.ShouldNotBeNull();
        row.FailedOn.ShouldBeNull();
        row.LastError.ShouldBeNull();
    }

    private static void ShouldBeGivenBack(InboxRow row, TimeSpan handlerTimeout)
    {
        row.Attempts.ShouldBe(0);
        row.ProcessedOn.ShouldBeNull();
        row.FailedOn.ShouldBeNull();
        row.LastError.ShouldBeNull();
        row.NextAttemptOn.ShouldBeLessThan(row.ReceivedOn + 2 * handlerTimeout);
    }

    private static void ShouldBeLeased(InboxRow row, TimeSpan handlerTimeout)
    {
        row.Attempts.ShouldBe(1);
        row.ProcessedOn.ShouldBeNull();
        row.FailedOn.ShouldBeNull();
        row.LastError.ShouldBeNull();
        (row.NextAttemptOn - row.ReceivedOn).ShouldBeInRange(2 * handlerTimeout, 2 * handlerTimeout + ClaimDelayLimit);
    }

    private Task<String> CreateDatabaseAsync() =>
        TestHostEnvironment.CreateDatabaseAsync(postgresFixture.ConnectionString, "shutdown");

    private static Dictionary<String, String> CreateEnvironment(String connectionString, TimeSpan handlerTimeout)
    {
        var environment = TestHostEnvironment.Create(connectionString);
        environment["App__Queues__HandlerTimeout"] = FormatDuration(handlerTimeout);
        environment[PollIntervalKey] = FormatDuration(TimeSpan.FromMilliseconds(250));
        environment[CleanupInBackgroundKey] = "false";
        return environment;
    }

    private static String FormatDuration(TimeSpan duration) => duration.ToString("c", CultureInfo.InvariantCulture);

    private static async Task<InboxRow> ReadInboxRowAsync(String connectionString)
    {
        var rows = await new DatabaseProbe(connectionString).QueryAsync<InboxRow>(InboxRowSql);

        return rows.ShouldHaveSingleItem();
    }

    private static Task<Int64> CountPongsAsync(String connectionString) =>
        new DatabaseProbe(connectionString).ExecuteScalarAsync<Int64>(PongCountSql);

    private static async Task<Int64> CountEffectsAsync(String effectPath)
    {
        if (!File.Exists(effectPath))
        {
            return 0;
        }

        var effectLines = await File.ReadAllLinesAsync(effectPath, CancellationToken);
        return effectLines.Length;
    }

    private static Task InsertSecretRowAsync(String connectionString)
    {
        var messageId = Guid.CreateVersion7();
        var pingId = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(
            new Dictionary<String, String>
            {
                ["id"] = messageId.ToString(),
                ["occurredOn"] = "2026-09-28T08:00:00+00:00",
                ["pingId"] = pingId.ToString(),
                ["secret"] = Secret,
            });

        return new DatabaseProbe(connectionString).ExecuteAsync(
            InsertSecretRowSql,
            new { Id = messageId, Payload = payload });
    }
}
