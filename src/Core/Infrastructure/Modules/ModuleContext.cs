using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

public sealed class ModuleContext<TModule>(ModuleDbContext moduleDbContext, ModuleDescriptor moduleDescriptor)
    where TModule : IModuleFacade
{
    public ModuleDbContext DbContext { get; } = moduleDbContext;

    public ModuleDescriptor Descriptor { get; } = moduleDescriptor;
}
