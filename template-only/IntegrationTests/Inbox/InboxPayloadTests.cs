using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Inbox;

public sealed class InboxPayloadTests(PostgresFixture postgresFixture)
{
    private const String InsertSql = """
        INSERT INTO pongs.inbox_messages
            (id, type, payload, trace_parent, attempts, next_attempt_on, occurred_on, received_on)
        VALUES (@Id, 'pings.ping-created', @Payload::jsonb, NULL, 0, @ReceivedOn, @ReceivedOn, @ReceivedOn)
        """;

    private const String LastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE id = @Id";

    [Fact]
    public async Task Payload_without_an_id_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(
            testModulesHost,
            messageId,
            $$"""{"occurredOn":"2026-09-28T08:00:00+00:00","pingId":"{{messageId}}","label":"a"}""");

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var row = await ReadInboxRowAsync(testModulesHost, messageId);
        var lastError = await ReadLastErrorAsync(testModulesHost, messageId);
        var pongCount = await testModulesHost.CountPongsAsync();
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        lastError.ShouldStartWith("Messaging.InvalidPayload");
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Payload_without_occurredOn_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(
            testModulesHost,
            messageId,
            $$"""{"id":"{{messageId}}","pingId":"{{messageId}}","label":"a"}""");

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var row = await ReadInboxRowAsync(testModulesHost, messageId);
        var lastError = await ReadLastErrorAsync(testModulesHost, messageId);
        var pongCount = await testModulesHost.CountPongsAsync();
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        lastError.ShouldStartWith("Messaging.InvalidPayload");
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Payload_with_an_empty_id_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(testModulesHost, messageId, BuildPayload(Guid.Empty, messageId));

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var row = await ReadInboxRowAsync(testModulesHost, messageId);
        var lastError = await ReadLastErrorAsync(testModulesHost, messageId);
        var pongCount = await testModulesHost.CountPongsAsync();
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        lastError.ShouldStartWith("Messaging.ForeignPayload");
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Payload_with_another_messages_id_fails_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(testModulesHost, messageId, BuildPayload(Guid.CreateVersion7(), messageId));

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var row = await ReadInboxRowAsync(testModulesHost, messageId);
        var lastError = await ReadLastErrorAsync(testModulesHost, messageId);
        var pongCount = await testModulesHost.CountPongsAsync();
        row.HasFailed.ShouldBeTrue();
        lastError.ShouldStartWith("Messaging.ForeignPayload");
        pongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Payload_with_unknown_fields_is_processed()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var messageId = Guid.CreateVersion7();
        await InsertInboxRowAsync(
            testModulesHost,
            messageId,
            $$"""
            {
                "id": "{{messageId}}",
                "occurredOn": "2026-09-28T08:00:00+00:00",
                "pingId": "{{messageId}}",
                "label": "a",
                "added": 1
            }
            """);

        await testModulesHost.ProcessInboxAsync(TestContext.Current.CancellationToken);

        var row = await ReadInboxRowAsync(testModulesHost, messageId);
        row.IsProcessed.ShouldBeTrue();
    }

    private static String BuildPayload(Guid payloadId, Guid pingId) =>
        $$"""{"id":"{{payloadId}}","occurredOn":"2026-09-28T08:00:00+00:00","pingId":"{{pingId}}","label":"a"}""";

    private static Task InsertInboxRowAsync(TestModulesHost testModulesHost, Guid messageId, String payload) =>
        testModulesHost.Database.ExecuteAsync(
            InsertSql,
            new { Id = messageId, Payload = payload, ReceivedOn = testModulesHost.Time.GetUtcNow() });

    private static async Task<QueueRow> ReadInboxRowAsync(TestModulesHost testModulesHost, Guid messageId)
    {
        var rows = await testModulesHost.ReadInboxRowsAsync();

        return rows.Single(row => row.Id == messageId);
    }

    private static Task<String> ReadLastErrorAsync(TestModulesHost testModulesHost, Guid messageId) =>
        testModulesHost.Database.ExecuteScalarAsync<String>(LastErrorSql, new { Id = messageId });
}
