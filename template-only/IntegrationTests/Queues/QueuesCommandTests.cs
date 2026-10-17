using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class QueuesCommandTests(PostgresFixture postgresFixture)
{
    private const Int32 Done = 0;

    private const Int32 Failed = 1;

    private const Int32 UsageError = 2;

    private const Int32 Conflict = 3;

    private const String Pongs = "Pongs";

    private const String InboxMessages = "inbox_messages";

    private const String Header = "module\tqueue\tid\ttype\tattempts\tfailed_on\tcode";

    private const String Usage = "usage: queues list";

    private const String Missing = "is missing";

    private const String NotFailed = "has not failed";

    private const String Secret = "s3cret";

    private const String DottedSecret = "s3cret.token";

    private const String DottedSecretWithDetail = "s3cret.token: rejected";

    private const String JwtShapedError = "eyJhbGciOiJIUzI1NiJ9.eyJzZWNyZXQiOiJzM2NyZXQifQ.SflKxwRJSMeKKF2QT4fwpM";

    private const String PingCreatedType = "pings.ping-created";

    private const String PingSecretSentType = "pings.ping-secret-sent";

    private const String RejectedError = "Pong.Rejected: Pong refused this ping";

    private const String OutputFailure = "No space left on device";

    private const String FailingWrite = "write";

    private const String FailingFlush = "flush";

    private const String InsertOutboxRowSql = """
        INSERT INTO {0}.outbox_messages
            (id, type, payload, attempts, next_attempt_on, last_error, processed_on, failed_on, occurred_on)
        VALUES (@Id, @Type, @Payload::jsonb, @Attempts, @EndedOn, @LastError, @ProcessedOn, @FailedOn, @EndedOn)
        """;

    private const String InsertInboxRowSql = """
        INSERT INTO {0}.inbox_messages
            (id, type, payload, attempts, next_attempt_on, last_error, processed_on, failed_on,
             occurred_on, received_on, contains_secrets)
        VALUES (@Id, @Type, @Payload::jsonb, @Attempts, @EndedOn, @LastError, @ProcessedOn, @FailedOn,
                @EndedOn, @EndedOn, @ContainsSecrets)
        """;

    private const String InsertInternalCommandSql = """
        INSERT INTO {0}.internal_commands
            (id, type, payload, attempts, next_attempt_on, last_error, processed_on, failed_on, enqueued_on)
        VALUES (@Id, @Type, @Payload::jsonb, @Attempts, @EndedOn, @LastError, @ProcessedOn, @FailedOn, @EndedOn)
        """;

    private const String HeldRetrySql = """
        UPDATE pongs.inbox_messages SET attempts = 0, failed_on = NULL, next_attempt_on = now()
        WHERE id = '{0}' AND failed_on IS NOT NULL AND payload <> '{{}}'::jsonb
        """;

    private const String HeldCleanupSql = """
        DELETE FROM pongs.inbox_messages
        WHERE id IN (SELECT id FROM pongs.inbox_messages WHERE failed_on < '{0}' LIMIT 1000) AND failed_on < '{0}'
        """;

    private const String ColumnNamesSql = """
        SELECT column_name FROM information_schema.columns
        WHERE table_schema = @Schema AND table_name = @Queue
        """;

    private const String WaitingOnLockSql = """
        SELECT count(*) FROM pg_stat_activity
        WHERE datname = current_database() AND wait_event_type = 'Lock' AND starts_with(query, @Statement)
        """;

    private static readonly TimeSpan LockWaitDeadline = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan PastKeepFailedFor = TimeSpan.FromDays(31);

    private static readonly DateTimeOffset TestStartedOn = TestModulesHost.TestStartedOn;

    private static readonly Dictionary<String, String> InsertRowSql = new(StringComparer.Ordinal)
    {
        ["outbox_messages"] = InsertOutboxRowSql,
        [InboxMessages] = InsertInboxRowSql,
        ["internal_commands"] = InsertInternalCommandSql,
    };

    private static readonly JsonSerializerOptions SnakeCaseJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_prints_the_failed_messages_of_every_module_and_queue_and_nothing_else()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(5),
            PingCreatedType,
            attempts: 1,
            TestStartedOn.AddSeconds(2),
            RejectedError));

        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(3),
            PingCreatedType,
            attempts: 1,
            TestStartedOn.AddSeconds(2),
            RejectedError));

        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.FailedWithErasedPayload(
            CreateId(4),
            PingSecretSentType,
            attempts: 7,
            TestStartedOn.AddSeconds(1),
            "Handler.Exception"));

        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Pending(CreateId(7), TestStartedOn));
        await InsertAsync(testModulesHost, "pings", "outbox_messages", SeedRow.Failed(
            CreateId(1),
            PingCreatedType,
            attempts: 7,
            TestStartedOn.AddSeconds(3),
            "Messaging.UnknownType: pings.ping-created: no type of this name"));

        await InsertAsync(testModulesHost, "pings", "outbox_messages", SeedRow.Processed(CreateId(8), TestStartedOn));
        await InsertAsync(testModulesHost, "pings", "internal_commands", SeedRow.Failed(
            CreateId(2),
            "touch-ping",
            attempts: 8,
            TestStartedOn.AddSeconds(1),
            "Handler.Abandoned: Gave up after 8 attempts, the last did not finish"));

        await InsertAsync(testModulesHost, "pings", InboxMessages, SeedRow.Failed(
            CreateId(6),
            "pongs.pong-made",
            attempts: 2,
            TestStartedOn.AddSeconds(5),
            "Messaging.UnsubscribedType: Pings does not subscribe to pongs.pong-made"));

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        list.Error.ShouldBeEmpty();
        lines.ShouldBe(
        [
            Header,
            $"Pings\tinbox_messages\t{CreateId(6)}\tpongs.pong-made\t2\t2026-09-28T08:00:05.0000000Z"
            + "\tMessaging.UnsubscribedType",
            $"Pings\tinternal_commands\t{CreateId(2)}\ttouch-ping\t8\t2026-09-28T08:00:01.0000000Z\tHandler.Abandoned",
            $"Pings\toutbox_messages\t{CreateId(1)}\t{PingCreatedType}\t7\t2026-09-28T08:00:03.0000000Z"
            + "\tMessaging.UnknownType",
            $"Pongs\tinbox_messages\t{CreateId(4)}\t{PingSecretSentType}\t7\t2026-09-28T08:00:01.0000000Z"
            + "\tHandler.Exception",
            $"Pongs\tinbox_messages\t{CreateId(3)}\t{PingCreatedType}\t1\t2026-09-28T08:00:02.0000000Z\tPong.Rejected",
            $"Pongs\tinbox_messages\t{CreateId(5)}\t{PingCreatedType}\t1\t2026-09-28T08:00:02.0000000Z\tPong.Rejected",
        ]);
    }

    [Fact]
    public async Task List_prints_no_code_for_a_row_stored_before_core_87_whose_handler_threw()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(1),
            PingSecretSentType,
            attempts: 7,
            TestStartedOn,
            $"the handler saw {Secret}"));

        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(2),
            PingCreatedType,
            attempts: 1,
            TestStartedOn.AddSeconds(1),
            "line one\nline two\tafter a tab: more"));

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        list.Output.ShouldNotContain(Secret);
        lines.ShouldBe(
        [
            Header,
            $"Pongs\tinbox_messages\t{CreateId(1)}\t{PingSecretSentType}\t7\t2026-09-28T08:00:00.0000000Z\t",
            $"Pongs\tinbox_messages\t{CreateId(2)}\t{PingCreatedType}\t1\t2026-09-28T08:00:01.0000000Z\t",
        ]);
    }

    [Theory]
    [InlineData("validation: label is required", "validation")]
    [InlineData("scaffold.app-layout: the app layout is missing", "scaffold.app-layout")]
    [InlineData(RejectedError, "Pong.Rejected")]
    public async Task List_prints_the_code_of_an_ordinary_row_in_every_shape_of_a_code(String lastError, String code)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(1),
            PingCreatedType,
            attempts: 1,
            TestStartedOn,
            lastError));

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        lines.ShouldBe(
        [
            Header,
            $"Pongs\tinbox_messages\t{CreateId(1)}\t{PingCreatedType}\t1\t2026-09-28T08:00:00.0000000Z\t{code}",
        ]);
    }

    [Theory]
    [InlineData(Secret)]
    [InlineData(DottedSecret)]
    [InlineData(DottedSecretWithDetail)]
    [InlineData(JwtShapedError)]
    [InlineData("validation")]
    [InlineData("scaffold.app-layout")]
    public async Task List_prints_no_code_for_a_secret_row_that_kept_its_payload_whatever_its_error_text(
        String lastError)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            CreateId(1),
            PingSecretSentType,
            attempts: 7,
            TestStartedOn,
            lastError));

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        list.Output.ShouldNotContain(Secret);
        lines.ShouldBe(
        [
            Header,
            $"Pongs\tinbox_messages\t{CreateId(1)}\t{PingSecretSentType}\t7\t2026-09-28T08:00:00.0000000Z\t",
        ]);
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("scaffold.app-layout")]
    [InlineData("Handler.Exception")]
    public async Task List_prints_the_code_of_a_secret_row_whose_payload_was_erased(String code)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.FailedWithErasedPayload(
            CreateId(1),
            PingSecretSentType,
            attempts: 7,
            TestStartedOn,
            code));

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        lines.ShouldBe(
        [
            Header,
            $"Pongs\tinbox_messages\t{CreateId(1)}\t{PingSecretSentType}\t7\t2026-09-28T08:00:00.0000000Z\t{code}",
        ]);
    }

    [Fact]
    public async Task List_prints_only_the_header_when_no_message_has_failed()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("handled");
        await testModulesHost.ProcessQueuesAsync();

        var list = await RunAsync(testModulesHost, "list");

        var lines = ReadLines(list.Output);

        list.ExitCode.ShouldBe(Done, list.Error);
        lines.ShouldBe([Header]);
    }

    [Fact]
    public async Task Retry_makes_a_failed_message_due_with_no_attempts_and_the_pump_then_handles_it()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedInboxRowAsync(testModulesHost, "broken");
        testModulesHost.PongHooks.FailFor = [];
        testModulesHost.Time.Advance(TimeSpan.FromMinutes(5));
        var retriedOn = testModulesHost.Time.GetUtcNow();

        var retry = await RunAsync(testModulesHost, "retry", "pongs", InboxMessages, failedRow.Id.ToString());

        var retriedRow = await ReadInboxRowAsync(testModulesHost, failedRow.Id);
        await testModulesHost.ProcessInboxAsync(CancellationToken);
        var handledRow = await ReadInboxRowAsync(testModulesHost, failedRow.Id);
        var pongCount = await testModulesHost.CountPongsAsync();

        retry.ExitCode.ShouldBe(Done, retry.Error);
        retry.Output.ShouldBeEmpty();
        retry.Error.ShouldBeEmpty();
        failedRow.HasFailed.ShouldBeTrue();
        retriedRow.HasFailed.ShouldBeFalse();
        retriedRow.Attempts.ShouldBe(0);
        retriedRow.NextAttemptOn.ShouldBe(retriedOn.UtcDateTime);
        retriedRow.Payload.ShouldBe(failedRow.Payload);
        handledRow.IsProcessed.ShouldBeTrue();
        handledRow.Attempts.ShouldBe(1);
        pongCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("retry", "pending", NotFailed)]
    [InlineData("retry", "processed", NotFailed)]
    [InlineData("retry", "missing", Missing)]
    [InlineData("discard", "pending", NotFailed)]
    [InlineData("discard", "processed", NotFailed)]
    [InlineData("discard", "missing", Missing)]
    public async Task Retry_or_discard_of_a_message_that_has_not_failed_is_a_conflict_that_changes_nothing(
        String action,
        String state,
        String reason)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Pending(CreateId(1), TestStartedOn));
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Processed(CreateId(2), TestStartedOn));
        var messageId = state switch
        {
            "pending" => CreateId(1),
            "processed" => CreateId(2),
            _ => CreateId(9),
        };

        var rowsBefore = await testModulesHost.ReadInboxRowsAsync();

        var run = await RunAsync(testModulesHost, action, Pongs, InboxMessages, messageId.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        run.ExitCode.ShouldBe(Conflict);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain($"Pongs inbox_messages {messageId} {reason}");
        rowsAfter.ShouldBe(rowsBefore);
    }

    [Fact]
    public async Task Retry_of_a_secret_message_whose_payload_was_erased_is_a_conflict_that_names_the_erased_payload()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedSecretInboxRowAsync(testModulesHost);
        testModulesHost.PongHooks.FailFor = [];

        var rowsBefore = await testModulesHost.ReadInboxRowsAsync();

        var retry = await RunAsync(testModulesHost, "retry", Pongs, InboxMessages, failedRow.Id.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        failedRow.HasFailed.ShouldBeTrue();
        failedRow.Payload.ShouldBe("{}");
        retry.ExitCode.ShouldBe(Conflict);
        retry.Output.ShouldBeEmpty();
        retry.Error.ShouldContain($"Pongs inbox_messages {failedRow.Id} has an erased payload");
        rowsAfter.ShouldBe(rowsBefore);
    }

    [Fact]
    public async Task Discard_prints_the_row_as_one_json_line_and_deletes_it()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedInboxRowAsync(testModulesHost, "broken");

        var discard = await RunAsync(testModulesHost, "discard", Pongs, InboxMessages, failedRow.Id.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();
        var discardedRow = ReadDiscardedRow(discard.Output);
        var label = discardedRow.Payload["label"].GetString();

        discard.ExitCode.ShouldBe(Done, discard.Error);
        discard.Error.ShouldBeEmpty();
        discardedRow.Id.ShouldBe(failedRow.Id);
        discardedRow.Type.ShouldBe(PingCreatedType);
        label.ShouldBe("broken");
        discardedRow.Attempts.ShouldBe(1);
        discardedRow.LastError.ShouldBe(RejectedError);
        discardedRow.FailedOn.ShouldBe("2026-09-28T08:00:00.0000000Z");
        discardedRow.ProcessedOn.ShouldBeNull();
        discardedRow.ContainsSecrets.ShouldBeFalse();
        rowsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task Discard_of_a_secret_message_prints_its_erased_payload_as_an_empty_object()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedSecretInboxRowAsync(testModulesHost);

        var discard = await RunAsync(testModulesHost, "discard", Pongs, InboxMessages, failedRow.Id.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();
        var discardedRow = ReadDiscardedRow(discard.Output);

        discard.ExitCode.ShouldBe(Done, discard.Error);
        discardedRow.Id.ShouldBe(failedRow.Id);
        discardedRow.Payload.ShouldBeEmpty();
        discardedRow.LastError.ShouldBe("Pong.SecretRefused");
        discardedRow.ContainsSecrets.ShouldBeTrue();
        discard.Output.ShouldNotContain(Secret);
        rowsAfter.ShouldNotContain(row => row.Id == failedRow.Id);
    }

    [Theory]
    [InlineData(PingSecretSentType, false, "Handler.Exception")]
    [InlineData(SecretOfALaterReleaseIntegrationEvent.Name, true, "Messaging.UnknownType")]
    public async Task Discard_of_a_secret_row_that_kept_its_secret_prints_neither_its_payload_nor_its_error(
        String type,
        Boolean containsSecrets,
        String code)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = CreateId(1);
        var secretPayload = JsonSerializer.Serialize(
            new Dictionary<String, String> { ["id"] = messageId.ToString(), ["secret"] = Secret });

        await InsertAsync(testModulesHost, "pongs", InboxMessages, new SeedRow(messageId, type, 7, TestStartedOn)
        {
            Payload = secretPayload,
            LastError = $"{code}: the handler saw {Secret}",
            FailedOn = TestStartedOn,
            ContainsSecrets = containsSecrets,
        });

        var discard = await RunAsync(testModulesHost, "discard", Pongs, InboxMessages, messageId.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();
        var discardedRow = ReadDiscardedRow(discard.Output);

        discard.ExitCode.ShouldBe(Done, discard.Error);
        discard.Output.ShouldNotContain(Secret);
        discardedRow.Id.ShouldBe(messageId);
        discardedRow.Type.ShouldBe(type);
        discardedRow.Payload.ShouldBeEmpty();
        discardedRow.LastError.ShouldBeNull();
        discardedRow.ContainsSecrets.ShouldBe(containsSecrets);
        rowsAfter.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("scaffold.app-layout")]
    [InlineData("Handler.Exception")]
    public async Task Discard_of_a_secret_row_whose_payload_was_erased_prints_its_code(String code)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = CreateId(1);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.FailedWithErasedPayload(
            messageId,
            PingSecretSentType,
            attempts: 7,
            TestStartedOn,
            code));

        var discard = await RunAsync(testModulesHost, "discard", Pongs, InboxMessages, messageId.ToString());

        var discardedRow = ReadDiscardedRow(discard.Output);

        discard.ExitCode.ShouldBe(Done, discard.Error);
        discardedRow.Payload.ShouldBeEmpty();
        discardedRow.LastError.ShouldBe(code);
    }

    [Theory]
    [InlineData(Secret)]
    [InlineData(DottedSecret)]
    [InlineData(DottedSecretWithDetail)]
    [InlineData(JwtShapedError)]
    [InlineData("validation")]
    [InlineData("scaffold.app-layout")]
    [InlineData("the handler saw s3cret")]
    [InlineData("card refused: s3cret")]
    [InlineData("line one\nline two: s3cret")]
    public async Task Discard_of_a_secret_row_stored_before_core_87_prints_no_error_text_at_all(String lastError)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = CreateId(1);

        await InsertAsync(testModulesHost, "pongs", InboxMessages, new SeedRow(
            messageId,
            PingSecretSentType,
            7,
            TestStartedOn)
        {
            Payload = JsonSerializer.Serialize(new Dictionary<String, String> { ["secret"] = Secret }),
            LastError = lastError,
            FailedOn = TestStartedOn,
        });

        var discard = await RunAsync(testModulesHost, "discard", Pongs, InboxMessages, messageId.ToString());

        var discardedRow = ReadDiscardedRow(discard.Output);

        discard.ExitCode.ShouldBe(Done, discard.Error);
        discard.Output.ShouldNotContain(Secret);
        discardedRow.Payload.ShouldBeEmpty();
        discardedRow.LastError.ShouldBeNull();
    }

    [Theory]
    [InlineData("outbox_messages")]
    [InlineData(InboxMessages)]
    [InlineData("internal_commands")]
    public async Task Discard_prints_every_column_of_the_table_the_row_was_in(String queue)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = CreateId(1);
        await InsertAsync(testModulesHost, "pings", queue, SeedRow.Failed(
            messageId,
            PingCreatedType,
            attempts: 3,
            TestStartedOn,
            RejectedError));

        var columns = await testModulesHost.Database.QueryAsync<String>(
            ColumnNamesSql,
            new { Schema = "pings", Queue = queue });

        var discard = await RunAsync(testModulesHost, "discard", "pings", queue, messageId.ToString());

        var lines = ReadLines(discard.Output);
        var printedLine = lines.ShouldHaveSingleItem();
        var printedKeys = JsonDocument
            .Parse(printedLine)
            .RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToList();

        discard.ExitCode.ShouldBe(Done, discard.Error);
        columns.ShouldNotBeEmpty();
        printedKeys.ShouldBe(columns, ignoreOrder: true);
    }

    [Theory]
    [InlineData(FailingWrite)]
    [InlineData(FailingFlush)]
    public async Task Discard_that_cannot_write_its_output_fails_and_keeps_the_row(String failingStep)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedInboxRowAsync(testModulesHost, "broken");
        using var failingTextWriter = new FailingTextWriter(failingStep, OutputFailure);

        var discard = await RunAsync(
            testModulesHost,
            failingTextWriter,
            "discard",
            Pongs,
            InboxMessages,
            failedRow.Id.ToString());

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        discard.ExitCode.ShouldBe(Failed);
        discard.Error.ShouldContain(OutputFailure);
        rowsAfter.ShouldBe([failedRow]);
    }

    [Theory]
    [InlineData("", "no command given")]
    [InlineData("purge", "unknown command 'purge'")]
    [InlineData("list everything", "list takes no arguments")]
    [InlineData("retry Pongs inbox_messages", "retry takes a module, a queue and a message id")]
    [InlineData(
        "discard Pongs inbox_messages 01999a00-0000-7000-8000-000000000001 now",
        "discard takes a module, a queue and a message id")]
    [InlineData(
        "retry Nobody inbox_messages 01999a00-0000-7000-8000-000000000001",
        "unknown module 'Nobody'; the modules are Pings, Pongs")]
    [InlineData(
        "retry Pongs mail_messages 01999a00-0000-7000-8000-000000000001",
        "unknown queue 'mail_messages'; the queues are inbox_messages, internal_commands, outbox_messages")]
    [InlineData("discard Pongs inbox_messages forty-two", "'forty-two' is not a message id")]
    public async Task Usage_error_prints_the_reason_and_the_usage_and_changes_nothing(
        String commandLine,
        String reason)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var failedRow = await CreateFailedInboxRowAsync(testModulesHost, "broken");
        var arguments = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var run = await RunAsync(testModulesHost, arguments);

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        run.ExitCode.ShouldBe(UsageError);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain(reason);
        run.Error.ShouldContain(Usage);
        rowsAfter.ShouldBe([failedRow]);
    }

    [Fact]
    public async Task Unknown_module_in_an_app_without_modules_is_a_usage_error_that_says_no_modules_are_registered()
    {
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton(new ModuleRegistry())
            .BuildServiceProvider();

        using var outputTextWriter = new StringWriter(CultureInfo.InvariantCulture);
        using var errorTextWriter = new StringWriter(CultureInfo.InvariantCulture);
        var queuesTool = new QueuesTool(serviceProvider, outputTextWriter, errorTextWriter);

        var exitCode = await queuesTool.RunAsync(
            ["retry", "Nobody", InboxMessages, $"{CreateId(1)}"],
            CancellationToken);

        var output = outputTextWriter.ToString();
        var error = errorTextWriter.ToString();

        exitCode.ShouldBe(UsageError);
        output.ShouldBeEmpty();
        error.ShouldContain("unknown module 'Nobody'; no modules are registered");
        error.ShouldContain(Usage);
    }

    [Fact]
    public async Task Retry_that_waits_for_a_discard_is_a_conflict_once_the_discard_commits()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        using var blockingTextWriter = new BlockingTextWriter();
        var discard = RunAsync(testModulesHost, blockingTextWriter, "discard", Pongs, InboxMessages, $"{messageId}");
        Boolean isRetryWaiting;
        Task<QueuesRun> retry;
        try
        {
            await blockingTextWriter.FlushReached.WaitAsync(LockWaitDeadline, CancellationToken);
            retry = RunAsync(testModulesHost, "retry", Pongs, InboxMessages, messageId.ToString());
            isRetryWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, retry, "UPDATE");
        }
        finally
        {
            blockingTextWriter.Release();
        }

        var discardRun = await discard;
        var retryRun = await retry;
        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        isRetryWaiting.ShouldBeTrue();
        discardRun.ExitCode.ShouldBe(Done, discardRun.Error);
        retryRun.ExitCode.ShouldBe(Conflict);
        retryRun.Error.ShouldContain($"{messageId} {Missing}");
        rowsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task Discard_that_waits_for_a_retry_is_a_conflict_once_the_retry_commits()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        await using var retry = await UncommittedUpdate.ExecuteAsync(
            ResolvePongsDataSource(testModulesHost),
            String.Format(CultureInfo.InvariantCulture, HeldRetrySql, messageId));

        var discard = RunAsync(testModulesHost, "discard", Pongs, InboxMessages, messageId.ToString());
        var isDiscardWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, discard, "DELETE");
        await retry.CommitAsync();
        var discardRun = await discard;

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        isDiscardWaiting.ShouldBeTrue();
        discardRun.ExitCode.ShouldBe(Conflict);
        discardRun.Output.ShouldBeEmpty();
        discardRun.Error.ShouldContain($"{messageId} {NotFailed}");
        var retriedRow = rowsAfter.ShouldHaveSingleItem();
        retriedRow.HasFailed.ShouldBeFalse();
        retriedRow.Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task Cleanup_that_waits_for_a_discard_counts_nothing_for_the_discarded_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        testModulesHost.Time.Advance(PastKeepFailedFor);
        using var blockingTextWriter = new BlockingTextWriter();
        var discard = RunAsync(testModulesHost, blockingTextWriter, "discard", Pongs, InboxMessages, $"{messageId}");
        Boolean isCleanupWaiting;
        Task<Int32> cleanup;
        try
        {
            await blockingTextWriter.FlushReached.WaitAsync(LockWaitDeadline, CancellationToken);
            cleanup = testModulesHost.RunCleanupAsync();
            isCleanupWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, cleanup, "DELETE");
        }
        finally
        {
            blockingTextWriter.Release();
        }

        var discardRun = await discard;
        var deletedCount = await cleanup;
        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();
        var discardedRow = ReadDiscardedRow(discardRun.Output);

        isCleanupWaiting.ShouldBeTrue();
        discardRun.ExitCode.ShouldBe(Done, discardRun.Error);
        discardedRow.Id.ShouldBe(messageId);
        deletedCount.ShouldBe(0);
        rowsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task Discard_that_waits_for_a_cleanup_is_a_conflict_once_the_cleanup_commits()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        await using var cleanup = await UncommittedUpdate.ExecuteAsync(
            ResolvePongsDataSource(testModulesHost),
            BuildHeldCleanupSql());

        var discard = RunAsync(testModulesHost, "discard", Pongs, InboxMessages, messageId.ToString());
        var isDiscardWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, discard, "DELETE");
        await cleanup.CommitAsync();
        var discardRun = await discard;

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        isDiscardWaiting.ShouldBeTrue();
        discardRun.ExitCode.ShouldBe(Conflict);
        discardRun.Output.ShouldBeEmpty();
        discardRun.Error.ShouldContain($"{messageId} {Missing}");
        rowsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cleanup_that_waits_for_a_retry_keeps_the_retried_row()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        testModulesHost.Time.Advance(PastKeepFailedFor);
        await using var retry = await UncommittedUpdate.ExecuteAsync(
            ResolvePongsDataSource(testModulesHost),
            String.Format(CultureInfo.InvariantCulture, HeldRetrySql, messageId));

        var cleanup = testModulesHost.RunCleanupAsync();
        var isCleanupWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, cleanup, "DELETE");
        await retry.CommitAsync();
        var deletedCount = await cleanup;

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        isCleanupWaiting.ShouldBeTrue();
        deletedCount.ShouldBe(0);
        var retriedRow = rowsAfter.ShouldHaveSingleItem();
        retriedRow.Id.ShouldBe(messageId);
        retriedRow.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task Retry_that_waits_for_a_cleanup_is_a_conflict_once_the_cleanup_commits()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertFailedInboxRowAsync(testModulesHost);
        await using var cleanup = await UncommittedUpdate.ExecuteAsync(
            ResolvePongsDataSource(testModulesHost),
            BuildHeldCleanupSql());

        var retry = RunAsync(testModulesHost, "retry", Pongs, InboxMessages, messageId.ToString());
        var isRetryWaiting = await WaitUntilWaitingOnLockAsync(testModulesHost, retry, "UPDATE");
        await cleanup.CommitAsync();
        var retryRun = await retry;

        var rowsAfter = await testModulesHost.ReadInboxRowsAsync();

        isRetryWaiting.ShouldBeTrue();
        retryRun.ExitCode.ShouldBe(Conflict);
        retryRun.Error.ShouldContain($"{messageId} {Missing}");
        rowsAfter.ShouldBeEmpty();
    }

    private static Task<QueuesRun> RunAsync(TestModulesHost testModulesHost, params String[] arguments) =>
        RunAsync(testModulesHost, new StringWriter(CultureInfo.InvariantCulture), arguments);

    private static async Task<QueuesRun> RunAsync(
        TestModulesHost testModulesHost,
        StringWriter outputTextWriter,
        params String[] arguments)
    {
        using var errorTextWriter = new StringWriter(CultureInfo.InvariantCulture);
        var queuesTool = new QueuesTool(testModulesHost.Services, outputTextWriter, errorTextWriter);
        var exitCode = await queuesTool.RunAsync(arguments, CancellationToken);

        return new QueuesRun(exitCode, outputTextWriter.ToString(), errorTextWriter.ToString());
    }

    private static async Task<QueueRow> CreateFailedInboxRowAsync(TestModulesHost testModulesHost, String label)
    {
        testModulesHost.PongHooks.FailFor = [label];
        await testModulesHost.CreatePingAsync(label);
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        return inboxRows.Single(row => row.HasFailed);
    }

    private static async Task<QueueRow> CreateFailedSecretInboxRowAsync(TestModulesHost testModulesHost)
    {
        testModulesHost.PongHooks.FailFor = [Secret];
        var pingId = await testModulesHost.CreatePingAsync("secret holder");
        var sent = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, Secret),
            CancellationToken);

        sent.IsSuccess.ShouldBeTrue();
        await testModulesHost.ProcessQueuesAllowingFailuresAsync();

        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        return inboxRows.Single(row => row.Type == PingSecretSentType);
    }

    private static async Task<Guid> InsertFailedInboxRowAsync(TestModulesHost testModulesHost)
    {
        var messageId = CreateId(1);
        await InsertAsync(testModulesHost, "pongs", InboxMessages, SeedRow.Failed(
            messageId,
            PingCreatedType,
            attempts: 1,
            TestStartedOn,
            RejectedError));

        return messageId;
    }

    private static Task InsertAsync(TestModulesHost testModulesHost, String schema, String queue, SeedRow row) =>
        testModulesHost.Database.ExecuteAsync(
            String.Format(CultureInfo.InvariantCulture, InsertRowSql[queue], schema),
            row);

    private static async Task<QueueRow> ReadInboxRowAsync(TestModulesHost testModulesHost, Guid messageId)
    {
        var inboxRows = await testModulesHost.ReadInboxRowsAsync();

        return inboxRows.Single(row => row.Id == messageId);
    }

    private static NpgsqlDataSource ResolvePongsDataSource(TestModulesHost testModulesHost) =>
        testModulesHost.Services.GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPongsModule));

    private static String BuildHeldCleanupSql()
    {
        var deleteFailedBefore = TestStartedOn
            .AddDays(1)
            .ToString("O", CultureInfo.InvariantCulture);

        return String.Format(CultureInfo.InvariantCulture, HeldCleanupSql, deleteFailedBefore);
    }

    private static async Task<Boolean> WaitUntilWaitingOnLockAsync(
        TestModulesHost testModulesHost,
        Task loser,
        String statement)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!loser.IsCompleted && stopwatch.Elapsed < LockWaitDeadline)
        {
            var waitingCount = await testModulesHost.Database.ExecuteScalarAsync<Int64>(
                WaitingOnLockSql,
                new { Statement = statement });

            if (waitingCount > 0)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }

        return false;
    }

    private static String[] ReadLines(String output) =>
        output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private static DiscardedRow ReadDiscardedRow(String output)
    {
        var lines = ReadLines(output);
        var line = lines.ShouldHaveSingleItem();
        var discardedRow = JsonSerializer.Deserialize<DiscardedRow>(line, SnakeCaseJsonSerializerOptions);

        return discardedRow.ShouldNotBeNull();
    }

    private static Guid CreateId(Int32 number) =>
        Guid.Parse($"01999a00-0000-7000-8000-{number.ToString("D12", CultureInfo.InvariantCulture)}");

    private sealed record QueuesRun(Int32 ExitCode, String Output, String Error);

    private sealed record DiscardedRow(
        Guid Id,
        String Type,
        Dictionary<String, JsonElement> Payload,
        Int32 Attempts,
        String? LastError,
        String? ProcessedOn,
        String? FailedOn,
        Boolean ContainsSecrets);

    private sealed record SeedRow(Guid Id, String Type, Int32 Attempts, DateTimeOffset EndedOn)
    {
        public String Payload { get; init; } = "{}";

        public String? LastError { get; init; }

        public DateTimeOffset? ProcessedOn { get; init; }

        public DateTimeOffset? FailedOn { get; init; }

        public Boolean ContainsSecrets { get; init; }

        public static SeedRow Failed(
            Guid id,
            String type,
            Int32 attempts,
            DateTimeOffset failedOn,
            String lastError) =>
            new(id, type, attempts, failedOn)
            {
                Payload = BuildPayload(id),
                LastError = lastError,
                FailedOn = failedOn,
            };

        public static SeedRow FailedWithErasedPayload(
            Guid id,
            String type,
            Int32 attempts,
            DateTimeOffset failedOn,
            String code) =>
            Failed(id, type, attempts, failedOn, code) with { Payload = "{}" };

        public static SeedRow Pending(Guid id, DateTimeOffset dueOn) =>
            new(id, "pings.ping-created", 0, dueOn) { Payload = BuildPayload(id) };

        public static SeedRow Processed(Guid id, DateTimeOffset processedOn) =>
            new(id, "pings.ping-created", 1, processedOn)
            {
                Payload = BuildPayload(id),
                ProcessedOn = processedOn,
            };

        private static String BuildPayload(Guid id) =>
            JsonSerializer.Serialize(new Dictionary<String, String> { ["id"] = id.ToString(), ["label"] = "seeded" });
    }

    private sealed class FailingTextWriter(String failingStep, String failure)
        : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Task WriteLineAsync(String? value) =>
            failingStep == FailingWrite
                ? Task.FromException(new IOException(failure))
                : base.WriteLineAsync(value);

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            failingStep == FailingFlush
                ? Task.FromException(new IOException(failure))
                : base.FlushAsync(cancellationToken);
    }

    private sealed class BlockingTextWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
        private readonly TaskCompletionSource _flushReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FlushReached => _flushReached.Task;

        public void Release() => _released.TrySetResult();

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            _flushReached.TrySetResult();
            await _released.Task;
            await base.FlushAsync(cancellationToken);
        }
    }
}
