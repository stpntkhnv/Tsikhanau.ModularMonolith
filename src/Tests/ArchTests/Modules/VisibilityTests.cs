using Microsoft.AspNetCore.Components;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.Shared.Reflection;
using ModularMonolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Modules;

public sealed class VisibilityTests
{
    [Theory]
    [EveryModule]
    public void Handlers_are_internal_and_sealed(String name)
    {
        var handlerTypes = ArchModules
            .Get(name).Handlers
            .ToList();

        Assert.SkipWhen(handlerTypes.Count == 0, $"{name} has no handlers");

        handlerTypes
            .Where(IsOpenToOthers)
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Module_exposes_at_most_its_module_class_outside_its_contracts(String name)
    {
        var module = ArchModules.Get(name);
        var typesOutsideContracts = module
            .GetTypesOutside("Contracts")
            .ToList();

        Assert.SkipWhen(typesOutsideContracts.Count == 0, $"{name} has no types outside Contracts");

        var exposedTypeNames = typesOutsideContracts
            .Where(type => type.IsVisible)
            .Where(type => !IsGeneratedPublic(type))
            .Select(type => type.FullName)
            .Where(typeName => typeName != BuildModuleClassName(module));

        exposedTypeNames.ShouldBeEmpty();
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Module_assembly_exposes_only_its_module_class(String name)
    {
        var module = ArchModules.Get(name);

        var exposedTypeNames = ArchModules
            .LoadModuleAssembly(name).HandwrittenTypes
            .Where(type => type.IsVisible)
            .Where(type => !IsGeneratedPublic(type))
            .Select(type => type.FullName)
            .Where(typeName => typeName != BuildModuleClassName(module));

        exposedTypeNames.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Contracts_are_public(String name)
    {
        var contractTypes = ArchModules
            .Get(name)
            .GetTypesIn("Contracts")
            .Where(type => !type.IsNested)
            .ToList();

        Assert.SkipWhen(contractTypes.Count == 0, $"{name} has no contracts");

        contractTypes
            .Where(type => !type.IsPublic)
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Endpoints_are_internal_and_sealed(String name)
    {
        var endpointTypes = ArchModules
            .Get(name).Types
            .Implementing<IEndpoint>()
            .ToList();

        Assert.SkipWhen(endpointTypes.Count == 0, $"{name} has no endpoints");

        endpointTypes
            .Where(IsOpenToOthers)
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    private static Boolean IsOpenToOthers(Type type) => type.IsVisible || !type.IsSealed;

    private static String BuildModuleClassName(ArchModule module) => $"{module.RootNamespace}.{module.Name}Module";

    private static Boolean IsGeneratedPublic(Type type) => IsMigration(type) || IsComponent(type);

    private static Boolean IsComponent(Type type) => typeof(IComponent).IsAssignableFrom(type);

    private static Boolean IsMigration(Type type) =>
        type.Namespace is { } typeNamespace && typeNamespace.EndsWith(".Migrations", StringComparison.Ordinal);
}
