using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingLoopedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, Int32 RemainingRounds)
    : DomainEvent(OccurredOn);
