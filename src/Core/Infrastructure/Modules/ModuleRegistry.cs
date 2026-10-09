namespace Tsikhanau.Monolith.Infrastructure.Modules;

public sealed class ModuleRegistry
{
    private readonly List<ModuleDescriptor> _modules = [];

    public IReadOnlyList<ModuleDescriptor> Modules => _modules;

    public void Add(ModuleDescriptor module)
    {
        if (_modules.Any(existingModule => existingModule.Key == module.Key || existingModule.Schema == module.Schema))
        {
            throw new InvalidOperationException($"Module {module.Name} (schema {module.Schema}) is registered twice");
        }

        _modules.Add(module);
    }
}
