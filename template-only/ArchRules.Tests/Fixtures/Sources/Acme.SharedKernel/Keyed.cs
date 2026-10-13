using Tsikhanau.Monolith.Application.Modules;

namespace Acme.SharedKernel;

public sealed class Keyed<TModule>
    where TModule : IModuleFacade
{
    public String Name => typeof(TModule).Name;
}
