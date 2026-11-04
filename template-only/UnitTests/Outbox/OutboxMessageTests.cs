using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Secrets;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;

namespace Tsikhanau.Monolith.UnitTests.Outbox;

public sealed class OutboxMessageTests
{
    private const String TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    private static readonly DateTimeOffset OccurredOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset AddedOn = OccurredOn.AddMinutes(5);

    [Fact]
    public void Row_of_a_secret_event_carries_the_event_its_secret_flag_its_trace_and_its_due_time()
    {
        var integrationEvent = new SecretTestIntegrationEvent(OccurredOn, "s3cret");

        var outboxMessage = OutboxMessage.Create(
            integrationEvent,
            new IntegrationEventRegistry(),
            AddedOn,
            TraceParent);

        outboxMessage.ShouldBeEquivalentTo(
            new OutboxMessage
            {
                Id = integrationEvent.Id,
                Type = "tests.secret",
                Payload = MessageSerializer.Serialize(integrationEvent),
                ContainsSecrets = true,
                TraceParent = TraceParent,
                NextAttemptOn = AddedOn,
                OccurredOn = OccurredOn,
            });
    }

    [Fact]
    public void Row_of_a_plain_event_carries_the_event_without_a_secret_flag_or_a_trace()
    {
        var integrationEvent = new PlainTestIntegrationEvent(OccurredOn, "plain");

        var outboxMessage = OutboxMessage.Create(
            integrationEvent,
            new IntegrationEventRegistry(),
            OccurredOn,
            traceParent: null);

        outboxMessage.ShouldBeEquivalentTo(
            new OutboxMessage
            {
                Id = integrationEvent.Id,
                Type = "tests.plain",
                Payload = MessageSerializer.Serialize(integrationEvent),
                ContainsSecrets = false,
                TraceParent = null,
                NextAttemptOn = OccurredOn,
                OccurredOn = OccurredOn,
            });
    }

    [IntegrationEventName("tests.secret")]
    [ContainsSecrets]
    private sealed record SecretTestIntegrationEvent(DateTimeOffset OccurredOn, String Secret)
        : IntegrationEvent(OccurredOn);

    [IntegrationEventName("tests.plain")]
    private sealed record PlainTestIntegrationEvent(DateTimeOffset OccurredOn, String Label)
        : IntegrationEvent(OccurredOn);
}
