using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Acme.Modules.Peer.Contracts.IntegrationEvents;

[IntegrationEventName("peer.peer-joined")]
public sealed record PeerJoinedIntegrationEvent(DateTimeOffset OccurredOn, Guid PeerId) : IntegrationEvent(OccurredOn);
