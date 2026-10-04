using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-secret-sent")]
[ContainsSecrets]
public sealed record PingSecretSentIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Secret)
    : IntegrationEvent(OccurredOn);
