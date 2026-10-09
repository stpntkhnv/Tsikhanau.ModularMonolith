using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Secrets;

namespace Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-secret-sent")]
[ContainsSecrets]
public sealed record PingSecretSentIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Secret)
    : IntegrationEvent(OccurredOn);
