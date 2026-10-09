using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Support;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public sealed class ArchModules
{
    private readonly Lazy<Architecture> _architecture;

    public ArchModules(IReadOnlyList<ArchModule> modules)
    {
        All = modules;
        _architecture = new Lazy<Architecture>(LoadArchitecture);
    }

    public IReadOnlyList<ArchModule> All { get; }

    internal IEnumerable<Assembly> Assemblies =>
        All
            .SelectMany(module => module.Assemblies)
            .Distinct();

    internal IEnumerable<Type> Types => All.SelectMany(module => module.Types);

    internal Architecture Architecture => _architecture.Value;

    public ArchModule Get(String name) => All.Single(module => module.Name == name);

    internal IEnumerable<ArchModule> GetOthers(ArchModule module) =>
        All.Where(otherModule => otherModule.Name != module.Name);

    private Architecture LoadArchitecture() =>
        Assemblies
            .Concat(CoreAssemblies.BuildingBlocks)
            .LoadArchitecture();
}
