using Acme.Modules.Peer.Contracts;
using Acme.Modules.Peer.Contracts.IntegrationEvents;
using Acme.Modules.Peer.Contracts.Queries.GetPeerName;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.PeerJoined;

internal sealed class PeerJoinedIntegrationEventHandler(IPeerQueryFacade peerQueryFacade)
    : IIntegrationEventHandler<PeerJoinedIntegrationEvent>
{
    public Task<Result<Unit>> Handle(
        PeerJoinedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        peerQueryFacade
            .ExecuteQueryAsync(new GetPeerNameQuery(integrationEvent.PeerId), cancellationToken)
            .MapAsync(_ => Unit.Value);
}
