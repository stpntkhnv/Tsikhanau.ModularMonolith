using Acme.Modules.Peer;
using Tsikhanau.Monolith.Infrastructure.Dispatching;

namespace Acme.Modules.Bad.Features.NeighbourCrate;

internal sealed class NeighbourDispatchService(ModuleDispatcher<IPeerModule> neighbourDispatcher)
{
    public Int32 Describe() => neighbourDispatcher.GetHashCode();
}
