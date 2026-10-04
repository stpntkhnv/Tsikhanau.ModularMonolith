using System.Reflection;
using System.Text.RegularExpressions;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Support;

public sealed record ArchModule(String Name, String RootNamespace, IReadOnlyList<Assembly> Assemblies)
{
    public static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IDomainEventHandler<>),
        typeof(IIntegrationEventHandler<>),
    ];

    public IReadOnlyList<Type> Types =>
        Assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(Contains)
            .Distinct()
            .ToList();

    public IEnumerable<Type> Handlers => Types.Where(IsHandler);

    public Boolean HasTypes => Types.Count > 0;

    public String RootPattern => $"^{Regex.Escape(RootNamespace)}(\\..+)?$";

    public String OutsideContractsPattern => $"^{Regex.Escape(RootNamespace)}(?!\\.Contracts(\\.|$))(\\..+)?$";

    public IEnumerable<Type> GetTypesIn(String layer) => Types.Where(type => Contains(type, layer));

    public IEnumerable<Type> GetTypesOutside(String layer) => Types.Where(type => ContainsOutside(type, layer));

    public Boolean Contains(Type type) => type.IsInNamespace(RootNamespace);

    public Boolean Contains(Type type, String layer) => type.IsInNamespace($"{RootNamespace}.{layer}");

    public Boolean ContainsOutside(Type type, String layer) => Contains(type) && !Contains(type, layer);

    public Boolean HasTypesIn(String layer) =>
        GetTypesIn(layer)
            .Any();

    public String BuildLayerPattern(params IEnumerable<String> layers) =>
        $"^{Regex.Escape(RootNamespace)}\\.({String.Join('|', layers.Select(Regex.Escape))})(\\..+)?$";

    private static Boolean IsHandler(Type type) =>
        HandlerInterfaces.Any(handlerInterfaceType => type.Implements(handlerInterfaceType));
}
