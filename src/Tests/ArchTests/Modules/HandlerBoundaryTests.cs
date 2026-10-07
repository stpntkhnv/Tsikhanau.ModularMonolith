using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.ArchTests.Modules;

public sealed class HandlerBoundaryTests
{
    private const String SampleNamespace = "ModularMonolith.ArchTests.Modules";
    private const String SampleQueryHandlerName = $"{SampleNamespace}.HandlerBoundaryTests+SampleQueryHandler";

    private const BindingFlags InstanceConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    [Theory]
    [EveryModule]
    public void Module_takes_no_handler_in_a_constructor(String name)
    {
        var module = ArchModules.Get(name);

        Assert.SkipUnless(module.HasTypes, $"{name} has no types");

        module.Types
            .SelectMany(GetHandlerParameters)
            .ShouldBeEmpty();
    }

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
        var module = ArchModules.Get(name);

        Assert.SkipWhen(GetQueryHandlers(module).Count == 0, $"{name} has no query handlers");

        ReadQueryHandlerReferences(module)
            .ShouldBeEmpty("A query handler only reads; take ISqlConnectionFactory, not the write side");
    }

    [Fact]
    public void Write_side_taken_by_a_type_nested_in_a_query_handler_is_checked()
    {
        var sampleModule = new ArchModule("Samples", SampleNamespace, [typeof(HandlerBoundaryTests).Assembly]);

        ReadQueryHandlerReferences(sampleModule)
            .ShouldContain($"{SampleQueryHandlerName}+OutboxWriter takes IOutbox`1 outbox");
    }

    [Theory]
    [EveryModule]
    public void Only_endpoints_and_background_jobs_take_the_module_facade(String name)
    {
        var module = ArchModules.Get(name);
        var facadeType = module.Types.Single(type => type.IsInterface && type.Name == $"I{name}Module");
        var subjects = module.Types
            .Where(type => module.Contains(type, "Domain") || module.Contains(type, "Features"))
            .Where(type => !type.IsCompilerGenerated && !IsEdge(type.TopLevelType))
            .ToList();

        Assert.SkipWhen(subjects.Count == 0, $"{name} has no Domain or Features types");

        subjects
            .SelectMany(type => GetReferences(type, referencedType => referencedType == facadeType))
            .ShouldBeEmpty("A handler that calls its own facade leaves its transaction; call the domain directly");
    }

    private static List<Type> GetQueryHandlers(ArchModule module) =>
        module.Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .ToList();

    private static List<String> ReadQueryHandlerReferences(ArchModule module)
    {
        var queryHandlers = GetQueryHandlers(module);

        return module.Types
            .Where(type => !type.IsCompilerGenerated && IsOrIsNestedIn(type, queryHandlers))
            .SelectMany(type => GetReferences(type, IsWriteSide))
            .ToList();
    }

    private static Boolean IsOrIsNestedIn(Type type, List<Type> handlers) =>
        handlers.Contains(type) || type.DeclaringType is { } declaringType && IsOrIsNestedIn(declaringType, handlers);

    private static Boolean IsEdge(Type type) => type.Implements<IEndpoint>() || type.Implements<IHostedService>();

    private static IEnumerable<String> GetReferences(Type subjectType, Func<Type, Boolean> isForbidden) =>
        subjectType.WithCompilerGeneratedNestedTypes
            .SelectMany(GetTypeUses)
            .Where(typeUse => IsOrWraps(typeUse.Type, isForbidden))
            .Select(typeUse => $"{subjectType.FullName} {typeUse.Description}");

    private static IEnumerable<TypeUse> GetTypeUses(Type type)
    {
        var methods = type
            .GetConstructors(AllDeclaredMembers)
            .Concat<MethodBase>(type.GetMethods(AllDeclaredMembers))
            .ToList();

        var parameters = methods
            .SelectMany(method => method.GetParameters())
            .Select(parameter => new TypeUse(parameter.ParameterType, "takes", parameter.Name));

        var locals = methods.SelectMany(GetLocalUses);

        var fields = type
            .GetFields(AllDeclaredMembers)
            .Select(field => new TypeUse(field.FieldType, "holds", field.Name));

        var properties = type
            .GetProperties(AllDeclaredMembers)
            .Select(property => new TypeUse(property.PropertyType, "holds", property.Name));

        return [.. parameters, .. locals, .. fields, .. properties];
    }

    private static IEnumerable<TypeUse> GetLocalUses(MethodBase method) =>
        (method.GetMethodBody()?.LocalVariables ?? [])
            .Select(local => new TypeUse(local.LocalType, "uses", $"in {method.DeclaringType?.Name}.{method.Name}"));

    private static Boolean IsOrWraps(Type type, Func<Type, Boolean> isForbidden) =>
        type.WithWrappedTypes
            .Any(isForbidden);

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

    private sealed record TypeUse(Type Type, String Verb, String? Name)
    {
        public String Description => $"{Verb} {Type.Name} {Name}";
    }

    private interface ISampleModule : IModuleFacade;

    private sealed record SampleQuery : IQuery<String>;

    private sealed class SampleQueryHandler : IQueryHandler<SampleQuery, String>
    {
        public Task<Result<String>> Handle(SampleQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<Result<String>>("read");

        private sealed class OutboxWriter(IOutbox<ISampleModule> outbox)
        {
            public IOutbox<ISampleModule> Outbox => outbox;
        }
    }
}
