using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pings.Features.IssueToken;
using ModularMonolith.TestModules.Pings.Features.MailToken;
using ModularMonolith.TestModules.Pings.Features.SendSecret;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class SecretPayloadTests(PostgresFixture postgresFixture)
{
    private const String PingSecretSentType = "pings.ping-secret-sent";
    private const String PingTokenIssuedType = "pings.ping-token-issued";
    private const String PingCreatedType = "pings.ping-created";
    private const String ErasedPayload = "{}";

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

    private static async Task<QueueRow> GetSingleRowAsync(Task<IReadOnlyList<QueueRow>> rows, String type) =>
        (await rows).Single(row => row.Type == type);
}
