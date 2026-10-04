using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingTouchedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, Int32 Touches)
    : DomainEvent(OccurredOn);
