using Acme.Modules.Peer;

namespace Acme.Modules.Bad.Infrastructure.Neighbours;

internal static class PeerProbe
{
    public static Boolean Knows(PeerModule peerModule) => peerModule is not null;
}
