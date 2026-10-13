using Tsikhanau.Monolith.Infrastructure.Dispatching;

namespace Acme.Modules.Bad.Features.DispatchCrate;

internal sealed class CrateDispatchService(ModuleDispatcher<IBadModule> moduleDispatcher)
{
    public Int32 Describe() => moduleDispatcher.GetHashCode();
}
