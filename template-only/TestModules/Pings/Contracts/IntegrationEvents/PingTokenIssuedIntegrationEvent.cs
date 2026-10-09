using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Secrets;

namespace Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-token-issued")]
[ContainsSecrets]
public sealed record PingTokenIssuedIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Token)
    : IntegrationEvent(OccurredOn);
