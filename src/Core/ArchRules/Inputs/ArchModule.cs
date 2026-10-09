using System.Reflection;
using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Shared.Reflection;
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

    internal IEnumerable<Assembly> ProjectAssemblies => GetAssembliesNamed(RootNamespace);

    internal String ContractsName => $"{RootNamespace}.Contracts";

    internal IEnumerable<Assembly> ContractsAssemblies => GetAssembliesNamed(ContractsName);

    internal IEnumerable<Assembly> OwnAssemblies => Assemblies.Where(IsNamedAfterTheModule);

    internal String RootPattern => $"^{Regex.Escape(RootNamespace)}(\\..+)?$";

    internal String OutsideContractsPattern => $"^{Regex.Escape(RootNamespace)}(?!\\.Contracts(\\.|$))(\\..+)?$";

    internal IEnumerable<Type> GetTypesIn(String layer) => Types.Where(type => Contains(type, layer));

    internal IEnumerable<Type> GetTypesOutside(String layer) => Types.Where(type => ContainsOutside(type, layer));

    internal Boolean Contains(Type type) => type.IsInNamespace(RootNamespace);

    internal Boolean Contains(Type type, String layer) => type.IsInNamespace($"{RootNamespace}.{layer}");

    internal Boolean ContainsOutside(Type type, String layer) => Contains(type) && !Contains(type, layer);

    internal String BuildLayerPattern(params IEnumerable<String> layers) =>
        $"^{Regex.Escape(RootNamespace)}\\.({String.Join('|', layers.Select(Regex.Escape))})(\\..+)?$";

    private static Boolean IsHandler(Type type) =>
        HandlerInterfaces.Any(handlerInterfaceType => type.Implements(handlerInterfaceType));

    private IReadOnlyList<Type> ReadTypes() =>
    [
        .. Assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(Contains)
            .Distinct(),
    ];

    private IEnumerable<Assembly> GetAssembliesNamed(String assemblyName) =>
        Assemblies.Where(assembly => assembly.Name == assemblyName);

    private Boolean IsNamedAfterTheModule(Assembly assembly) =>
        assembly.Name == RootNamespace || assembly.Name.StartsWith($"{RootNamespace}.", StringComparison.Ordinal);
}
