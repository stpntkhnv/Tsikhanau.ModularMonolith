using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingCreatedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, PingLabel Label)
    : DomainEvent(OccurredOn);
