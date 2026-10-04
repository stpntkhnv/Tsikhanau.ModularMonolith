using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.SendSecret;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Queues;

public sealed class SecretPayloadTests(PostgresFixture postgresFixture)
{
    private const String PingSecretSentType = "pings.ping-secret-sent";
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

    private static async Task<QueueRow> GetSingleRowAsync(Task<IReadOnlyList<QueueRow>> rows, String type) =>
        (await rows).Single(row => row.Type == type);
}
