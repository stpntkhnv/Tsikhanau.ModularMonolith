using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record EchoRelayedDomainEvent(
    DateTimeOffset OccurredOn,
    PingId PingId,
    PingId TargetPingId,
    EchoId EchoId,
    String Text)
    : DomainEvent(OccurredOn);
