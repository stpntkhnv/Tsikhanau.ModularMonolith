using Microsoft.AspNetCore.Components;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class VisibilityRules
{
    public static RuleEvaluation HandlersAreInternalAndSealed(ArchModule module) =>
        module.Handlers.CollectViolations(IsOpenToOthers);

    public static RuleEvaluation ModuleExposesAtMostItsModuleClassOutsideItsContracts(ArchModule module) =>
        module
            .GetTypesOutside("Contracts")
            .CollectViolations(type => IsExposedBesidesTheModuleClass(module, type));

    public static RuleEvaluation ModuleAssemblyExposesOnlyItsModuleClass(ArchModule module) =>
        module.ProjectAssemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .CollectViolations(type => IsExposedBesidesTheModuleClass(module, type));

    public static RuleEvaluation ContractsArePublic(ArchModule module) =>
        module
            .GetTypesIn("Contracts")
            .Where(type => !type.IsNested)
            .CollectViolations(type => !type.IsPublic);

    public static RuleEvaluation EndpointsAreInternalAndSealed(ArchModule module) =>
        module.Types
            .Implementing<IEndpoint>()
            .CollectViolations(IsOpenToOthers);

    private static Boolean IsOpenToOthers(Type type) => type.IsVisible || !type.IsSealed;

    private static Boolean IsExposedBesidesTheModuleClass(ArchModule module, Type type) =>
        type.IsVisible && !IsGeneratedPublic(type) && type.FullName != BuildModuleClassName(module);

    private static String BuildModuleClassName(ArchModule module) => $"{module.RootNamespace}.{module.Name}Module";

    private static Boolean IsGeneratedPublic(Type type) => IsMigration(type) || IsComponent(type);

    private static Boolean IsComponent(Type type) => typeof(IComponent).IsAssignableFrom(type);

    private static Boolean IsMigration(Type type) =>
        type.Namespace is { } typeNamespace && typeNamespace.EndsWith(".Migrations", StringComparison.Ordinal);
}
