using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Reflection;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Infrastructure.Persistence.Commits;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules.Dependencies;

public static class HandlerBoundaryRules
{
    private const BindingFlags InstanceConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static RuleEvaluation ModuleTakesNoHandlerInAConstructor(ArchModule module) =>
        module.Types.CollectViolations(GetHandlerParameters);

    public static RuleEvaluation QueryMarkerIsImplementedOnlyInsideItsModule(ArchModules modules, ArchModule module) =>
        RuleEvaluation.Create(module.Types, () => GetQueryMarkerViolations(modules, module));

    public static RuleEvaluation QueryHandlersTakeNoWriteSideDependencies(ArchModule module)
    {
        IReadOnlyList<Type> queryHandlers = [.. module.QueryHandlers];
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

        return modules.Assemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .Where(type => type.IsConcrete)
            .Where(type => !module.Contains(type))
            .Where(type => queryMarkers.Any(queryMarker => type.Implements(queryMarker)))
            .Select(type => type.FullNameOrName);
    }

    private static IEnumerable<String> GetFacadeReferences(ArchModule module, IReadOnlyList<Type> subjectTypes)
    {
        var facadeTypes = module.Types
            .Where(type => type.IsInterface && type.Name == module.FacadeName)
            .ToList();

        return facadeTypes.Count == 0
            ? [$"{module.Name} has no facade {module.FacadeName}"]
            : subjectTypes.SelectMany(type => GetReferences(type, wrapped => IsFacade(wrapped, facadeTypes)));
    }

    private static Boolean IsFacade(Type type, List<Type> facadeTypes) =>
        facadeTypes.Contains(type)
            || type.IsConstructedGenericType && ModuleKeyTypes.Facades.Contains(type.GetGenericTypeDefinition());

    private static IEnumerable<String> ReadQueryHandlerReferences(
        ArchModule module,
        IReadOnlyList<Type> queryHandlers) =>
        module.Types
            .Where(type => !type.IsCompilerGenerated && IsOrIsNestedIn(type, queryHandlers))
            .SelectMany(type => GetReferences(type, IsWriteSide));

    private static Boolean IsOrIsNestedIn(Type type, IReadOnlyList<Type> handlers) =>
        handlers.Contains(type) || type.DeclaringType is { } declaringType && IsOrIsNestedIn(declaringType, handlers);

    private static Boolean IsEdge(Type type) => type.Implements<IEndpoint>() || type.Implements<IHostedService>();

    private static IEnumerable<String> GetReferences(Type subjectType, Func<Type, Boolean> isForbidden) =>
        subjectType.WithCompilerGeneratedNestedTypes
            .SelectMany(GetTypeUses)
            .Where(typeUse => IsOrWraps(typeUse.Type, isForbidden))
            .Select(typeUse => $"{subjectType.FullName} {typeUse.Verb} {typeUse.Type.Name} {typeUse.Name}");

    private static IEnumerable<TypeUse> GetTypeUses(Type type) =>
        [.. type.ParameterUses, .. type.LocalUses, .. type.FieldUses, .. type.PropertyUses];

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
}
