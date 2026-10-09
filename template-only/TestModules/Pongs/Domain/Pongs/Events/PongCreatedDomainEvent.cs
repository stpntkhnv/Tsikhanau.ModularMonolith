using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs.Events;

internal sealed record PongCreatedDomainEvent(DateTimeOffset OccurredOn, PongId PongId) : DomainEvent(OccurredOn);
