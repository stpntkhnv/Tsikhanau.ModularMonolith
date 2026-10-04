using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.MixedTransaction;

[IntegrationEventName("tests.mixed-happened")]
public sealed record MixedHappenedIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
