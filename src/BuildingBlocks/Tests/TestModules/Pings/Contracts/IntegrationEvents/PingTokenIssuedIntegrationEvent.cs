using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-token-issued")]
[ContainsSecrets]
public sealed record PingTokenIssuedIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Token)
    : IntegrationEvent(OccurredOn);
