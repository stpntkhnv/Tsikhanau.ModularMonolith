using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.UnnamedIntegrationEvent;

public sealed record NamelessIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
