using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class HandlerBoundaryRules
{
    private const BindingFlags InstanceConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    public static RuleEvaluation ModuleTakesNoHandlerInAConstructor(ArchModule module) =>
        module.Types.CollectViolations(GetHandlerParameters);

    public static RuleEvaluation QueryMarkerIsImplementedOnlyInsideItsModule(ArchModules modules, ArchModule module) =>
        RuleEvaluation.Create(module.Types, () => GetQueryMarkerViolations(modules, module));

    public static RuleEvaluation QueryHandlersTakeNoWriteSideDependencies(ArchModule module)
    {
        var queryHandlers = GetQueryHandlers(module);
        return RuleEvaluation.Create(queryHandlers, () => ReadQueryHandlerReferences(module, queryHandlers));
    }

    public static RuleEvaluation OnlyEndpointsAndBackgroundJobsTakeTheModuleFacade(ArchModule module)
    {
        IReadOnlyList<Type> subjectTypes =
        [
            .. module.Types
                .Where(type => module.Contains(type, "Domain") || module.Contains(type, "Features"))
                .Where(type => !type.IsCompilerGenerated && !IsEdge(type.TopLevelType)),
        ];

        return RuleEvaluation.Create(subjectTypes, () => GetFacadeReferences(module, subjectTypes));
    }

    private static IEnumerable<String> GetQueryMarkerViolations(ArchModules modules, ArchModule module)
    {
        var queryMarkers = module
            .GetTypesIn("Contracts")
            .Where(IsQueryMarker)
            .ToList();

        var foreignQueryTypeNames = modules.Assemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .Where(type => type.IsConcrete)
            .Where(type => !module.Contains(type))
            .Where(type => queryMarkers.Any(queryMarker => type.Implements(queryMarker)))
            .Select(type => type.FullNameOrName);

        return queryMarkers.Count == 0
            ? [$"{module.Name} has no query marker in its Contracts (I{module.Name}Query<T> : IQuery<T>)"]
            : foreignQueryTypeNames;
    }

    private static IEnumerable<String> GetFacadeReferences(ArchModule module, IReadOnlyList<Type> subjectTypes)
    {
        var facadeTypes = module.Types
            .Where(type => type.IsInterface && type.Name == $"I{module.Name}Module")
            .ToList();

        return facadeTypes.Count == 0
            ? [$"{module.Name} has no facade I{module.Name}Module"]
            : subjectTypes.SelectMany(type => GetReferences(type, facadeTypes.Contains));
    }

    private static List<Type> GetQueryHandlers(ArchModule module) =>
        module.Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .ToList();

    private static IEnumerable<String> ReadQueryHandlerReferences(ArchModule module, List<Type> queryHandlers) =>
        module.Types
            .Where(type => !type.IsCompilerGenerated && IsOrIsNestedIn(type, queryHandlers))
            .SelectMany(type => GetReferences(type, IsWriteSide));

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
}
