using Tsikhanau.Monolith.Domain.Events;

namespace Acme.Modules.Bad.Contracts.Queries.CrateHistory;

public sealed record CrateHistoryDto(Guid CrateId, IReadOnlyList<DomainEvent> Events);
