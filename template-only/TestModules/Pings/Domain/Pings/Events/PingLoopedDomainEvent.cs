using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingLoopedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, Int32 RemainingRounds)
    : DomainEvent(OccurredOn);
