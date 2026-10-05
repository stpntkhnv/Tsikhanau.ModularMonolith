using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed class HandlerBoundaryTests
{
    private const BindingFlags InstanceConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

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

    [Theory]
    [EveryModule]
    public void Query_handlers_take_no_write_side_dependencies(String name)
    {
        var queryHandlers = ArchModules
            .Get(name).Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .ToList();

        Assert.SkipWhen(queryHandlers.Count == 0, $"{name} has no query handlers");

        queryHandlers
            .SelectMany(type => GetMembersTyped(type, IsWriteSide))
            .ShouldBeEmpty("A query handler only reads; take ISqlConnectionFactory, not the write side");
    }

    [Theory]
    [EveryModule]
    public void Only_endpoints_and_background_jobs_take_the_module_facade(String name)
    {
        var module = ArchModules.Get(name);
        var facadeType = module.Types.Single(type => type.IsInterface && type.Name == $"I{name}Module");
        var subjects = module.Types
            .Where(type => module.Contains(type, "Domain") || module.Contains(type, "Features"))
            .Where(type => !IsEdge(type.TopLevelType))
            .ToList();

        Assert.SkipWhen(subjects.Count == 0, $"{name} has no Domain or Features types");

        subjects
            .SelectMany(type => GetMembersTyped(type, memberType => memberType == facadeType))
            .ShouldBeEmpty("A handler that calls its own facade leaves its transaction; call the domain directly");
    }

    private static Boolean IsEdge(Type type) => type.Implements<IEndpoint>() || type.Implements<IHostedService>();

    private static IEnumerable<String> GetMembersTyped(Type type, Func<Type, Boolean> isForbidden)
    {
        var parameters = type
            .GetConstructors(InstanceConstructors)
            .Concat<MethodBase>(type.GetMethods(AllDeclaredMembers))
            .SelectMany(method => method.GetParameters())
            .Where(parameter => isForbidden(parameter.ParameterType))
            .Select(parameter => $"{type.FullName} takes {parameter.ParameterType.Name} {parameter.Name}");

        var fields = type
            .GetFields(AllDeclaredMembers)
            .Where(field => isForbidden(field.FieldType))
            .Select(field => $"{type.FullName} holds {field.FieldType.Name} {field.Name}");

        var properties = type
            .GetProperties(AllDeclaredMembers)
            .Where(property => isForbidden(property.PropertyType))
            .Select(property => $"{type.FullName} holds {property.PropertyType.Name} {property.Name}");

        return [.. parameters, .. fields, .. properties];
    }

    private static Boolean IsWriteSide(Type type) =>
        type.IsBuiltFrom(typeof(IUnitOfWork<>))
            || type.IsBuiltFrom(typeof(IOutbox<>))
            || type.IsBuiltFrom(typeof(IInternalCommandScheduler<>))
            || type.Implements<DbContext>()
            || type.Name.EndsWith("Repository", StringComparison.Ordinal);

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
