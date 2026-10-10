using Acme.Modules.Peer.Contracts;

namespace Acme.Modules.Bad.Features.StealQuery;

internal sealed record StolenPeerQuery(Guid PeerId) : IPeerQuery<String>;
