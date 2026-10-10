using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Acme.Modules.Bad.Contracts.IntegrationEvents;

[IntegrationEventName("peer.peer-joined")]
public sealed record CrateCreatedIntegrationEvent(DateTimeOffset OccurredOn, Guid CrateId)
    : IntegrationEvent(OccurredOn);
