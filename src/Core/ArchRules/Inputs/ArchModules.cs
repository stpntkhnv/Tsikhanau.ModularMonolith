using System.Reflection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public sealed class ArchModules
{
    private static readonly Assembly[] CoreAssemblies =
    [
        typeof(AggregateRoot<>).Assembly,
        typeof(IntegrationEvent).Assembly,
        typeof(IModuleFacade).Assembly,
        typeof(ModuleDbContext).Assembly,
        typeof(IEndpoint).Assembly,
    ];

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
            .Concat(CoreAssemblies)
            .LoadArchitecture();
}
