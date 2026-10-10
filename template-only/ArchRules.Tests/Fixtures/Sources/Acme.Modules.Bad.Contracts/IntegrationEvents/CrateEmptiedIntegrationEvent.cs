using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Acme.Modules.Bad.Contracts.IntegrationEvents;

public sealed record CrateEmptiedIntegrationEvent(DateTimeOffset OccurredOn, Guid CrateId)
    : IntegrationEvent(OccurredOn);
