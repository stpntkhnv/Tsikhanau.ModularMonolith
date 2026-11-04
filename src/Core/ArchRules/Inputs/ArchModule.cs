using System.Reflection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.ArchUnit;
using Tsikhanau.Monolith.ArchRules.Reflection;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Monolith.Testing.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public sealed class ArchModule
{
    internal static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IDomainEventHandler<>),
        typeof(IIntegrationEventHandler<>),
    ];

    private readonly Lazy<LoadedAssemblies> _loadedAssemblies;
    private readonly Lazy<IReadOnlyList<Type>> _types;

    public ArchModule(String name, String rootNamespace, Func<IReadOnlyList<Assembly>> loadAssemblies)
    {
        Name = name;
        RootNamespace = rootNamespace;
        _loadedAssemblies = new Lazy<LoadedAssemblies>(() => LoadedAssemblies.Load(loadAssemblies));
        _types = new Lazy<IReadOnlyList<Type>>(ReadTypes);
    }

    public String Name { get; }

    public String RootNamespace { get; }

    public IReadOnlyList<Assembly> Assemblies => _loadedAssemblies.Value.Assemblies;

    internal Optional<String> LoadFailure => _loadedAssemblies.Value.Failure;

    internal IReadOnlyList<Type> Types => _types.Value;

    internal IEnumerable<Type> Handlers => Types.Where(IsHandler);

    internal IEnumerable<Type> QueryHandlers => Types.Where(IsQueryHandler);

    internal IEnumerable<Assembly> ProjectAssemblies => GetAssembliesNamed(RootNamespace);

    internal IEnumerable<Assembly> ContractsAssemblies => GetAssembliesNamed(ContractsName);

    internal IEnumerable<Assembly> OwnAssemblies => Assemblies.Where(IsNamedAfterTheModule);

    internal String ContractsName => ModuleLayout.BuildContractsName(RootNamespace);

    internal IReadOnlyList<String> ProjectNames => [RootNamespace, ContractsName];

    internal String ModuleClassName => $"{RootNamespace}.{Name}Module";

    internal String FacadeName => $"I{Name}Module";

    internal String RootPattern => NamespacePatterns.Build(RootNamespace);

    internal String OutsideContractsPattern => NamespacePatterns.BuildExcept(RootNamespace, "Contracts");

    public static IReadOnlyList<ArchModule> ForProductionModules(String appRootNamespace) =>
    [
        .. ProductionModules.Names.Select(moduleName => CreateProductionModule(appRootNamespace, moduleName)),
    ];

    internal IEnumerable<Type> GetTypesIn(String layer) => Types.Where(type => Contains(type, layer));

    internal IEnumerable<Type> GetTypesOutside(String layer) => Types.Where(type => ContainsOutside(type, layer));

    internal Boolean Contains(Type type) => type.IsInNamespace(RootNamespace);

    internal Boolean Contains(Type type, String layer) => type.IsInNamespace($"{RootNamespace}.{layer}");

    internal Boolean ContainsOutside(Type type, String layer) => Contains(type) && !Contains(type, layer);

    internal String BuildLayerPattern(params IEnumerable<String> layers) =>
        NamespacePatterns.Build(layers.Select(layer => $"{RootNamespace}.{layer}"));

    private static ArchModule CreateProductionModule(String appRootNamespace, String moduleName)
    {
        var rootNamespace = ModuleLayout.BuildRootNamespace(appRootNamespace, moduleName);

        return new ArchModule(
            moduleName,
            rootNamespace,
            () => [Assembly.Load(rootNamespace), Assembly.Load(ModuleLayout.BuildContractsName(rootNamespace))]);
    }

    private static Boolean IsHandler(Type type) =>
        HandlerInterfaces.Any(handlerInterfaceType => type.Implements(handlerInterfaceType));

    private static Boolean IsQueryHandler(Type type) => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>));

    private IReadOnlyList<Type> ReadTypes() =>
    [
        .. _loadedAssemblies.Value.Types
            .Where(Contains)
            .Distinct(),
    ];

    private IEnumerable<Assembly> GetAssembliesNamed(String assemblyName) =>
        Assemblies.Where(assembly => assembly.Name == assemblyName);

    private Boolean IsNamedAfterTheModule(Assembly assembly) =>
        assembly.Name == RootNamespace || assembly.Name.StartsWith($"{RootNamespace}.", StringComparison.Ordinal);
}
