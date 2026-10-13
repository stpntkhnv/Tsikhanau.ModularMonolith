using Tsikhanau.Monolith.Infrastructure.Dispatching;

namespace Acme.Modules.Bad.Features.StrayCrate;

internal sealed class StrayDispatchService(ModuleDispatcher<IStrayFacade> strayDispatcher)
{
    public Int32 Describe() => strayDispatcher.GetHashCode();
}
