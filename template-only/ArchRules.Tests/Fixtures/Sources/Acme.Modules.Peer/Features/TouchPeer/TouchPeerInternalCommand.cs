using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Peer.Features.TouchPeer;

[InternalCommandName("touch")]
internal sealed record TouchPeerInternalCommand(Guid Id, Guid PeerId) : InternalCommand(Id);
