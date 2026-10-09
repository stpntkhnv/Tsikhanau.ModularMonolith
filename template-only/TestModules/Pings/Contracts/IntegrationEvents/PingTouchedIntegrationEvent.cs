using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-touched")]
public sealed record PingTouchedIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, Int32 Touches)
    : IntegrationEvent(OccurredOn);
