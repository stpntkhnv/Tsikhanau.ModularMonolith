using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Contracts.Secrets;

namespace ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;

[IntegrationEventName("pings.ping-secret-sent")]
[ContainsSecrets]
public sealed record PingSecretSentIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Secret)
    : IntegrationEvent(OccurredOn);
