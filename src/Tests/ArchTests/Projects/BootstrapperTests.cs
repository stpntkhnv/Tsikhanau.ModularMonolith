using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ModularMonolith.ArchTests.Support;
using Mono.Cecil;
using Tsikhanau.Monolith.Api.Modules;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.ArchTests.Projects;

public sealed class BootstrapperTests
{
    private const String DefaultsNamespace = "ModularMonolith.Bootstrapper.Defaults";

    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies([typeof(Program).Assembly, .. ArchModules.BuildingBlockAssemblies])
            .Build();

    [Fact]
    public void Service_defaults_do_not_depend_on_the_app()
    {
        var defaultsNamespaceTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace == DefaultsNamespace);

        var violations = Types()
            .That()
            .ResideInNamespaceMatching("^ModularMonolith\\.Bootstrapper\\.Defaults$")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^ModularMonolith(?!\\.Bootstrapper\\.Defaults$)(\\..+)?$")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^Tsikhanau\\.Monolith(\\..+)?$")
            .GetViolationsIn(Architecture);

        defaultsNamespaceTypes.ShouldNotBeEmpty();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Host_adds_every_module_folder()
    {
        var addedModuleNames = ReadModulesAddedByTheHost();
        Assert.SkipWhen(
            ArchModules.Production.Length == 0 && addedModuleNames.Count == 0,
            "There are no production modules");

        var expectedModuleNames = ArchModules.Production.Select(BuildModuleClassName);

        addedModuleNames.ShouldBe(expectedModuleNames, ignoreOrder: true);
    }

    private static String BuildModuleClassName(String name) =>
        $"ModularMonolith.Modules.{name}.{name}Module";

    private static List<String> ReadModulesAddedByTheHost()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(Program).Assembly.Location);
        return assembly.MainModule
            .GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<GenericInstanceMethod>()
            .Where(calledMethod => calledMethod.Name == nameof(ModuleHostExtensions.AddModule))
            .Select(calledMethod => calledMethod.GenericArguments.Single().FullName)
            .ToList();
    }
}
