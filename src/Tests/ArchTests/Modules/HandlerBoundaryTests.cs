using System.Reflection;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed class HandlerBoundaryTests
{
    private const BindingFlags InstanceConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [EveryModule]
    public void Module_takes_no_handler_in_a_constructor(String name) =>
        ArchModules
            .Get(name).Types
            .SelectMany(GetHandlerParameters)
            .ShouldBeEmpty();

    [Theory]
    [EveryModule]
    public void Query_marker_is_implemented_only_inside_its_module(String name)
    {
        var module = ArchModules.Get(name);
        var queryMarkers = module
            .GetTypesIn("Contracts")
            .Where(IsQueryMarker)
            .ToList();

        var foreignQueryTypeNames = ArchModules.ModuleAssemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .Where(type => type.IsConcrete)
            .Where(type => !module.Contains(type))
            .Where(type => queryMarkers.Any(queryMarker => type.Implements(queryMarker)))
            .Select(type => type.FullName);

        queryMarkers.ShouldNotBeEmpty();
        foreignQueryTypeNames.ShouldBeEmpty();
    }

    private static IEnumerable<String> GetHandlerParameters(Type type) =>
        type
            .GetConstructors(InstanceConstructors)
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => IsHandlerInterface(parameter.ParameterType))
            .Select(parameter => $"{type.FullName} takes {parameter.ParameterType} {parameter.Name}");

    private static Boolean IsHandlerInterface(Type type) =>
        ArchModule.HandlerInterfaces.Any(handlerInterfaceType => type.IsBuiltFrom(handlerInterfaceType));

    private static Boolean IsQueryMarker(Type type) =>
        type is { IsInterface: true, IsGenericTypeDefinition: true } && type.Implements(typeof(IQuery<>));
}
