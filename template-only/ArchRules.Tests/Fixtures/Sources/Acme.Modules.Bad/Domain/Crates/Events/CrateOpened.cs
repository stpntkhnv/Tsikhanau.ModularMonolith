using Tsikhanau.Monolith.Domain.Events;

namespace Acme.Modules.Bad.Domain.Crates.Events;

internal sealed record CrateOpened(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
