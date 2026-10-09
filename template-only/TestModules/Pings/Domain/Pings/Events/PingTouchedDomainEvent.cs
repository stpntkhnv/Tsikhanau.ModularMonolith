using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingTouchedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, Int32 Touches)
    : DomainEvent(OccurredOn);
