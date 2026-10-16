using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.IssueToken;
using Tsikhanau.Monolith.TestModules.Pings.Features.MailToken;
using Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class SecretPayloadTests(PostgresFixture postgresFixture)
{
    private const String PingSecretSentType = "pings.ping-secret-sent";
    private const String PingTokenIssuedType = "pings.ping-token-issued";
    private const String PingCreatedType = "pings.ping-created";
    private const String ErasedPayload = "{}";
    private const String MailTokenType = "mail-token";
    private const String TouchPingType = "touch-ping";
    private const String Secret = "s3cret";
    private const String InboxTable = "pongs.inbox_messages";
    private const String OutboxTable = "pings.outbox_messages";
    private const String InternalCommandsTable = "pings.internal_commands";
    private const String HandlerException = "Handler.Exception";
    private const String HandlerTimeout = "Handler.Timeout";
    private const String HandlerAbandoned = "Handler.Abandoned";
    private const String SecretBroken = "Pong.SecretBroken";
    private const String SecretRefused = "Pong.SecretRefused";
    private const Int32 MaxAttempts = 7;

    private const String ReadFlagSql = "SELECT contains_secrets FROM {0}.{1} WHERE type = @Type";

    private const String ReadLastErrorSql = "SELECT last_error FROM {0} WHERE id = @Id";

    private const String InsertInboxSql = """
        INSERT INTO pongs.inbox_messages
            (id, type, payload, attempts, next_attempt_on, occurred_on, received_on, contains_secrets)
        VALUES (@Id, @Type, @Payload::jsonb, @Attempts, @ReceivedOn, @ReceivedOn, @ReceivedOn, @ContainsSecrets)
        """;

    private const String InsertOutboxSql = """
        INSERT INTO pings.outbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, contains_secrets)
        VALUES (@Id, @Type, @Payload::jsonb, @Attempts, @OccurredOn, @OccurredOn, true)
        """;

    private const String InsertLegacyInboxSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@Id, @Type, @Payload::jsonb, 0, @ReceivedOn, @ReceivedOn, @ReceivedOn)
        """;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Secret_payload_leaves_the_outbox_once_delivered()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await SendSecretAsync(testModulesHost, "s3cret");

        await testModulesHost.DeliverOutboxAsync();

        var outboxRow = await GetSingleRowAsync(testModulesHost.ReadOutboxRowsAsync(), PingSecretSentType);
        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        outboxRow.IsProcessed.ShouldBeTrue();
        outboxRow.Payload.ShouldBe(ErasedPayload);
        inboxRow.Payload.ShouldContain("s3cret");
    }

    [Fact]
    public async Task Secret_payload_leaves_the_inbox_once_handled()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await SendSecretAsync(testModulesHost, "s3cret");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        inboxRow.IsProcessed.ShouldBeTrue();
        inboxRow.Payload.ShouldBe(ErasedPayload);
        testModulesHost.PongHooks.ReceivedSecrets.ShouldContain("s3cret");
    }

    [Theory]
    [InlineData(HandlerException, "Handler.Exception (InvalidOperationException)")]
    [InlineData(SecretBroken, SecretBroken)]
    [InlineData(HandlerTimeout, "Handler.Timeout (InvalidOperationException)")]
    public async Task Retried_secret_message_keeps_its_payload_and_only_the_code_of_its_failure(
        String code,
        String loggedFailure)
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var testModulesHost = await StartCapturingEveryLogAsync(capturingLoggerProvider);
        MakeSecretHandlingFail(testModulesHost, code);
        var messageId = await InsertSecretInboxRowAsync(testModulesHost, attempts: 0);

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        var lastError = await ReadLastErrorAsync(testModulesHost, InboxTable, messageId);
        inboxRow.HasFailed.ShouldBeFalse();
        inboxRow.Attempts.ShouldBe(1);
        inboxRow.Payload.ShouldContain(Secret);
        lastError.ShouldBe(code);
        capturingLoggerProvider.Entries.ShouldContain(entry => entry.Contains(loggedFailure));
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(Secret));
    }

    [Fact]
    public async Task Secret_payload_of_a_failed_message_is_erased_and_its_error_keeps_only_the_code()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var testModulesHost = await StartCapturingEveryLogAsync(capturingLoggerProvider);
        testModulesHost.PongHooks.FailFor = [Secret];
        await SendSecretAsync(testModulesHost, Secret);
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        var lastError = await ReadLastErrorAsync(testModulesHost, InboxTable, inboxRow.Id);
        inboxRow.HasFailed.ShouldBeTrue();
        inboxRow.Payload.ShouldBe(ErasedPayload);
        lastError.ShouldBe(SecretRefused);
        capturingLoggerProvider.Entries.ShouldContain(entry => entry.Contains(SecretRefused));
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(Secret));
    }

    [Theory]
    [InlineData(MaxAttempts - 1, HandlerException)]
    [InlineData(MaxAttempts, HandlerAbandoned)]
    public async Task Secret_message_whose_attempts_run_out_loses_its_payload_and_keeps_only_the_code(
        Int32 earlierAttempts,
        String code)
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var testModulesHost = await StartCapturingEveryLogAsync(capturingLoggerProvider);
        testModulesHost.PongHooks.WhileHandlingSecret = static _ => throw new InvalidOperationException(Secret);
        var messageId = await InsertSecretInboxRowAsync(testModulesHost, earlierAttempts);

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        var lastError = await ReadLastErrorAsync(testModulesHost, InboxTable, messageId);
        inboxRow.HasFailed.ShouldBeTrue();
        inboxRow.Payload.ShouldBe(ErasedPayload);
        lastError.ShouldBe(code);
        capturingLoggerProvider.Entries.ShouldContain(entry => entry.Contains(code));
        capturingLoggerProvider.Entries.ShouldNotContain(entry => entry.Contains(Secret));
    }

    [Fact]
    public async Task Secret_message_that_fails_once_and_then_succeeds_loses_its_payload_and_its_error()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.WhileHandlingSecret = static _ => throw new InvalidOperationException(Secret);
        var messageId = await InsertSecretInboxRowAsync(testModulesHost, attempts: 0);
        await testModulesHost.ProcessInboxAsync(CancellationToken);
        testModulesHost.PongHooks.WhileHandlingSecret = static _ => Task.CompletedTask;
        testModulesHost.Time.Advance(TimeSpan.FromSeconds(1));

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        var lastError = await ReadLastErrorAsync(testModulesHost, InboxTable, messageId);
        inboxRow.IsProcessed.ShouldBeTrue();
        inboxRow.Attempts.ShouldBe(2);
        inboxRow.Payload.ShouldBe(ErasedPayload);
        lastError.ShouldBeNull();
    }

    [Fact]
    public async Task Secret_row_of_a_type_this_instance_does_not_know_waits_with_its_payload_for_one_that_does()
    {
        await using var currentInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var laterInstance = await TestModulesHost.StartAsync(postgresFixture);
        laterInstance.Services
            .GetRequiredService<IntegrationEventRegistry>()
            .Register(typeof(SecretOfALaterReleaseIntegrationEvent));

        var messageId = await InsertSecretOfALaterReleaseAsync(currentInstance, attempts: 0);
        await currentInstance.DeliverOutboxAsync();
        var retriedRow = await GetSingleRowAsync(currentInstance.ReadOutboxRowsAsync(), messageId);
        var retriedLastError = await ReadLastErrorAsync(currentInstance, OutboxTable, messageId);
        laterInstance.Time.Advance(TimeSpan.FromSeconds(1));

        await laterInstance.DeliverOutboxAsync();

        var handledRow = await GetSingleRowAsync(laterInstance.ReadOutboxRowsAsync(), messageId);
        var handledLastError = await ReadLastErrorAsync(laterInstance, OutboxTable, messageId);
        retriedRow.HasFailed.ShouldBeFalse();
        retriedRow.Payload.ShouldContain(Secret);
        retriedLastError.ShouldBe("Messaging.UnknownType");
        handledRow.IsProcessed.ShouldBeTrue();
        handledRow.Payload.ShouldBe(ErasedPayload);
        handledLastError.ShouldBeNull();
    }

    [Fact]
    public async Task Secret_row_of_a_type_no_instance_knows_loses_its_payload_when_its_attempts_run_out()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = await InsertSecretOfALaterReleaseAsync(testModulesHost, attempts: MaxAttempts - 1);

        await testModulesHost.DeliverOutboxAsync();

        var outboxRow = await GetSingleRowAsync(testModulesHost.ReadOutboxRowsAsync(), messageId);
        var lastError = await ReadLastErrorAsync(testModulesHost, OutboxTable, messageId);
        outboxRow.HasFailed.ShouldBeTrue();
        outboxRow.Payload.ShouldBe(ErasedPayload);
        lastError.ShouldBe("Messaging.UnknownType");
    }

    [Fact]
    public async Task Secret_payload_nobody_subscribes_to_is_erased_by_an_instance_that_never_published_it()
    {
        await using var publishingInstance = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await publishingInstance.CreatePingAsync("token holder");
        var issueResult = await publishingInstance.Pings.ExecuteCommandAsync(
            new IssueTokenCommand(pingId, "t0ken"),
            CancellationToken);

        using var deliveringInstance = TestModulesHost.BuildUnstarted(postgresFixture.ConnectionString);

        await deliveringInstance.Services
            .GetRequiredService<OutboxPump<IPingsModule>>()
            .RunOnceAsync(CancellationToken);

        var outboxRow = await GetSingleRowAsync(publishingInstance.ReadOutboxRowsAsync(), PingTokenIssuedType);
        issueResult.IsSuccess.ShouldBeTrue();
        outboxRow.IsProcessed.ShouldBeTrue();
        outboxRow.Payload.ShouldBe(ErasedPayload);
    }

    [Fact]
    public async Task Secret_payload_of_a_scheduled_command_leaves_the_queue_once_handled()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await ScheduleTokenMailAsync(testModulesHost, "t0ken");

        await testModulesHost.RunInternalCommandsAsync();

        var rows = await testModulesHost.ReadInternalCommandRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.IsProcessed.ShouldBeTrue();
        row.Payload.ShouldBe(ErasedPayload);
        testModulesHost.PingHooks.MailedTokens.ShouldBe(["t0ken"]);
    }

    [Fact]
    public async Task Secret_payload_of_a_failed_scheduled_command_is_kept_for_the_retry_and_erased_with_its_error()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await ScheduleTokenMailAsync(testModulesHost, "t0ken");
        testModulesHost.PingHooks.AfterLoad = static () => throw new InvalidOperationException("Mail server is down");

        await testModulesHost.RunInternalCommandsAsync();

        var failedRows = await testModulesHost.ReadInternalCommandRowsAsync();
        var failedRow = failedRows.ShouldHaveSingleItem();
        var failedLastError = await ReadLastErrorAsync(testModulesHost, InternalCommandsTable, failedRow.Id);

        testModulesHost.PingHooks.AfterLoad = static () => Task.CompletedTask;
        testModulesHost.Time.Advance(TimeSpan.FromSeconds(1));
        await testModulesHost.RunInternalCommandsAsync();

        var retriedRows = await testModulesHost.ReadInternalCommandRowsAsync();
        var retriedLastError = await ReadLastErrorAsync(testModulesHost, InternalCommandsTable, failedRow.Id);

        failedRow.IsProcessed.ShouldBeFalse();
        failedRow.HasFailed.ShouldBeFalse();
        failedRow.Payload.ShouldContain("t0ken");
        failedLastError.ShouldBe(HandlerException);
        var retriedRow = retriedRows.ShouldHaveSingleItem();
        retriedRow.IsProcessed.ShouldBeTrue();
        retriedRow.Payload.ShouldBe(ErasedPayload);
        retriedLastError.ShouldBeNull();
        testModulesHost.PingHooks.MailedTokens.ShouldBe(["t0ken"]);
    }

    [Fact]
    public async Task Rows_of_a_secret_event_carry_the_secret_flag_and_ordinary_rows_do_not()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await SendSecretAsync(testModulesHost, "s3cret");
        await testModulesHost.DeliverOutboxAsync();

        var secretOutboxFlag = await ReadFlagAsync(testModulesHost, "pings", "outbox_messages", PingSecretSentType);
        var secretInboxFlag = await ReadFlagAsync(testModulesHost, "pongs", "inbox_messages", PingSecretSentType);
        var plainOutboxFlag = await ReadFlagAsync(testModulesHost, "pings", "outbox_messages", PingCreatedType);
        var plainInboxFlag = await ReadFlagAsync(testModulesHost, "pongs", "inbox_messages", PingCreatedType);

        secretOutboxFlag.ShouldBeTrue();
        secretInboxFlag.ShouldBeTrue();
        plainOutboxFlag.ShouldBeFalse();
        plainInboxFlag.ShouldBeFalse();
    }

    [Fact]
    public async Task Row_of_a_secret_command_carries_the_secret_flag_and_an_ordinary_command_does_not()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await ScheduleTokenMailAsync(testModulesHost, "t0ken");
        var pingId = await testModulesHost.CreatePingAsync("plain command");
        var scheduled = await testModulesHost.Pings.ExecuteCommandAsync(
            new ScheduleTouchCommand(pingId),
            CancellationToken);

        var secretFlag = await ReadFlagAsync(testModulesHost, "pings", "internal_commands", MailTokenType);
        var plainFlag = await ReadFlagAsync(testModulesHost, "pings", "internal_commands", TouchPingType);

        scheduled.IsSuccess.ShouldBeTrue();
        secretFlag.ShouldBeTrue();
        plainFlag.ShouldBeFalse();
    }

    [Fact]
    public async Task Secret_row_written_before_the_flag_existed_is_still_erased_once_handled()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await testModulesHost.Database.ExecuteAsync(
            InsertLegacyInboxSql,
            new
            {
                Id = messageId,
                Type = PingSecretSentType,
                Payload = BuildSecretPayload(messageId, "old-s3cret"),
                ReceivedOn = testModulesHost.Time.GetUtcNow(),
            });

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        inboxRow.IsProcessed.ShouldBeTrue();
        inboxRow.Payload.ShouldBe(ErasedPayload);
        testModulesHost.PongHooks.ReceivedSecrets.ShouldContain("old-s3cret");
    }

    [Fact]
    public async Task Row_flagged_secret_is_erased_once_handled_although_its_type_is_not_secret_here()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await testModulesHost.Database.ExecuteAsync(
            InsertInboxSql,
            new
            {
                Id = messageId,
                Type = PingCreatedType,
                Payload = BuildPingCreatedPayload(messageId, "flagged"),
                Attempts = 0,
                ReceivedOn = testModulesHost.Time.GetUtcNow(),
                ContainsSecrets = true,
            });

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingCreatedType);
        inboxRow.IsProcessed.ShouldBeTrue();
        inboxRow.Payload.ShouldBe(ErasedPayload);
    }

    [Fact]
    public async Task Ordinary_payload_is_kept_after_delivery_and_handling()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("plain");

        await testModulesHost.ProcessQueuesAsync();

        var outboxRow = await GetSingleRowAsync(testModulesHost.ReadOutboxRowsAsync(), PingCreatedType);
        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingCreatedType);
        outboxRow.Payload.ShouldContain("plain");
        inboxRow.Payload.ShouldContain("plain");
    }

    private static void MakeSecretHandlingFail(TestModulesHost testModulesHost, String code)
    {
        switch (code)
        {
            case HandlerException:
                testModulesHost.PongHooks.WhileHandlingSecret = static _ => throw new InvalidOperationException(Secret);
                break;
            case SecretBroken:
                testModulesHost.PongHooks.FailUnexpectedlyFor = [Secret];
                break;
            case HandlerTimeout:
                testModulesHost.PongHooks.WhileHandlingSecret = _ =>
                {
                    testModulesHost.Time.Advance(TestModulesHost.HandlerTimeout + TimeSpan.FromSeconds(1));
                    throw new InvalidOperationException(Secret);
                };

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(code), code, "No way to fail the secret with this code");
        }
    }

    private Task<TestModulesHost> StartCapturingEveryLogAsync(CapturingLoggerProvider capturingLoggerProvider) =>
        TestModulesHost.StartAsync(
            postgresFixture,
            configure: builder => builder.Logging
                .AddProvider(capturingLoggerProvider)
                .AddFilter<CapturingLoggerProvider>(category: null, LogLevel.Trace));

    private static async Task<Guid> InsertSecretInboxRowAsync(TestModulesHost testModulesHost, Int32 attempts)
    {
        var messageId = Guid.CreateVersion7();
        await testModulesHost.Database.ExecuteAsync(
            InsertInboxSql,
            new
            {
                Id = messageId,
                Type = PingSecretSentType,
                Payload = BuildSecretPayload(messageId, Secret),
                Attempts = attempts,
                ReceivedOn = testModulesHost.Time.GetUtcNow(),
                ContainsSecrets = true,
            });

        return messageId;
    }

    private static async Task<Guid> InsertSecretOfALaterReleaseAsync(TestModulesHost testModulesHost, Int32 attempts)
    {
        var messageId = Guid.CreateVersion7();
        await testModulesHost.Database.ExecuteAsync(
            InsertOutboxSql,
            new
            {
                Id = messageId,
                Type = SecretOfALaterReleaseIntegrationEvent.Name,
                Payload = BuildSecretPayload(messageId, Secret),
                Attempts = attempts,
                OccurredOn = testModulesHost.Time.GetUtcNow(),
            });

        return messageId;
    }

    private static Task<String?> ReadLastErrorAsync(TestModulesHost testModulesHost, String table, Guid messageId) =>
        testModulesHost.Database.ExecuteScalarAsync<String?>(
            String.Format(ReadLastErrorSql, table),
            new { Id = messageId });

    private static async Task SendSecretAsync(TestModulesHost testModulesHost, String secret)
    {
        var pingId = await testModulesHost.CreatePingAsync("secret holder");
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new SendSecretCommand(pingId, secret),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private static async Task ScheduleTokenMailAsync(TestModulesHost testModulesHost, String token)
    {
        var pingId = await testModulesHost.CreatePingAsync($"mail-{token}");
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new ScheduleTokenMailCommand(pingId, token),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private static Task<Boolean> ReadFlagAsync(
        TestModulesHost testModulesHost,
        String schema,
        String table,
        String type) =>
        testModulesHost.Database.ExecuteScalarAsync<Boolean>(
            String.Format(ReadFlagSql, schema, table),
            new { Type = type });

    private static String BuildSecretPayload(Guid messageId, String secret) =>
        BuildPayload(messageId, secret, "secret");

    private static String BuildPingCreatedPayload(Guid messageId, String label) =>
        BuildPayload(messageId, label, "label");

    private static String BuildPayload(Guid messageId, String value, String property) =>
        JsonSerializer.Serialize(
            new Dictionary<String, String>
            {
                ["id"] = messageId.ToString(),
                ["occurredOn"] = "2026-09-28T08:00:00+00:00",
                ["pingId"] = messageId.ToString(),
                [property] = value,
            });

    private static async Task<QueueRow> GetSingleRowAsync(Task<IReadOnlyList<QueueRow>> rows, String type) =>
        (await rows).Single(row => row.Type == type);

    private static async Task<QueueRow> GetSingleRowAsync(Task<IReadOnlyList<QueueRow>> rows, Guid messageId) =>
        (await rows).Single(row => row.Id == messageId);
}
