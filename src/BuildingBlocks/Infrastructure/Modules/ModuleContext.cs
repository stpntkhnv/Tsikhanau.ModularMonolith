using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

public sealed class ModuleContext<TModule>(ModuleDbContext moduleDbContext, ModuleDescriptor moduleDescriptor)
    where TModule : IModuleFacade
{
    public ModuleDbContext DbContext { get; } = moduleDbContext;

    public ModuleDescriptor Descriptor { get; } = moduleDescriptor;
}
