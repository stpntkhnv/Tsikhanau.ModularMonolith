using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.UnnamedIntegrationEvent;

public sealed record NamelessIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
