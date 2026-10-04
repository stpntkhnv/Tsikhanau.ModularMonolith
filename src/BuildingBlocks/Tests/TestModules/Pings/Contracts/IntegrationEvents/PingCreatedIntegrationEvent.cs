using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-created")]
public sealed record PingCreatedIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Label)
    : IntegrationEvent(OccurredOn);
