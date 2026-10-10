namespace Acme.Modules.Peer.Contracts.Queries.GetPeerName;

public sealed record GetPeerNameQuery(Guid PeerId) : IPeerQuery<String>;
