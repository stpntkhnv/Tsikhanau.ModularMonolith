using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.MixedTransaction;

[IntegrationEventName("tests.mixed-happened")]
public sealed record MixedHappenedIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
