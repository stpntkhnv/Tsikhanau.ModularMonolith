using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record EchoRelayedDomainEvent(
    DateTimeOffset OccurredOn,
    PingId PingId,
    PingId TargetPingId,
    EchoId EchoId,
    String Text)
    : DomainEvent(OccurredOn);
