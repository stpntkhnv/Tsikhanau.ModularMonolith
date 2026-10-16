using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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

    private const String ReadFlagSql = "SELECT contains_secrets FROM {0}.{1} WHERE type = @Type";

    private const String InsertInboxSql = """
        INSERT INTO pongs.inbox_messages
            (id, type, payload, attempts, next_attempt_on, occurred_on, received_on, contains_secrets)
        VALUES (@Id, @Type, @Payload::jsonb, 0, @ReceivedOn, @ReceivedOn, @ReceivedOn, @ContainsSecrets)
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

    [Fact]
    public async Task Secret_payload_of_a_failed_message_is_kept_for_inspection()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        testModulesHost.PongHooks.FailFor = ["doomed"];
        await SendSecretAsync(testModulesHost, "doomed");
        await testModulesHost.DeliverOutboxAsync();

        await testModulesHost.ProcessInboxAsync(CancellationToken);

        var inboxRow = await GetSingleRowAsync(testModulesHost.ReadInboxRowsAsync(), PingSecretSentType);
        inboxRow.HasFailed.ShouldBeTrue();
        inboxRow.Payload.ShouldContain("doomed");
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
    public async Task Secret_payload_of_a_failed_scheduled_command_is_kept_for_the_retry()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await ScheduleTokenMailAsync(testModulesHost, "t0ken");
        testModulesHost.PingHooks.AfterLoad = static () => throw new InvalidOperationException("Mail server is down");

        await testModulesHost.RunInternalCommandsAsync();

        var failedRows = await testModulesHost.ReadInternalCommandRowsAsync();

        testModulesHost.PingHooks.AfterLoad = static () => Task.CompletedTask;
        testModulesHost.Time.Advance(TimeSpan.FromSeconds(1));
        await testModulesHost.RunInternalCommandsAsync();

        var retriedRows = await testModulesHost.ReadInternalCommandRowsAsync();

        var failedRow = failedRows.ShouldHaveSingleItem();
        failedRow.IsProcessed.ShouldBeFalse();
        failedRow.HasFailed.ShouldBeFalse();
        failedRow.Payload.ShouldContain("t0ken");
        var retriedRow = retriedRows.ShouldHaveSingleItem();
        retriedRow.IsProcessed.ShouldBeTrue();
        retriedRow.Payload.ShouldBe(ErasedPayload);
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

    private static async Task SendSecretAsync(TestModulesHost testModulesHost, String secret)
    {
        var pingId = await testModulesHost.CreatePingAsync($"secret-{secret}");
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
}
