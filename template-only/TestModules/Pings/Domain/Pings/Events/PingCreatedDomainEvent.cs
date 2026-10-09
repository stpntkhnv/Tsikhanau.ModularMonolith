using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;

internal sealed record PingCreatedDomainEvent(DateTimeOffset OccurredOn, PingId PingId, PingLabel Label)
    : DomainEvent(OccurredOn);
