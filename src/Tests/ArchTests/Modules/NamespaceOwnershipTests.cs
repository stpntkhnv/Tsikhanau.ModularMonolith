using ModularMonolith.ArchTests.Support;
using ModularMonolith.Testing.Modules;
using Tsikhanau.Monolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed class NamespaceOwnershipTests
{
    private const String UnhandledFixtureNamespace = "Tsikhanau.Monolith.TestModules.Unhandled";

    private static readonly String[] TestModuleNamespaces =
    [
        .. ArchModules.TestOnly.Select(name => ArchModules.Get(name).RootNamespace),
        UnhandledFixtureNamespace,
    ];

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Module_assembly_holds_only_the_module_namespace_outside_its_contracts(String name)
    {
        var module = ArchModules.Get(name);

        var strayTypeNames = ArchModules
            .LoadModuleAssembly(name).HandwrittenTypes
            .Where(type => !module.ContainsOutside(type, "Contracts"))
            .Select(type => type.FullName);

        strayTypeNames.ShouldBeEmpty();
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Contracts_assembly_holds_only_the_contracts_namespace(String name)
    {
        var module = ArchModules.Get(name);

        var strayTypeNames = ArchModules
            .LoadContractsAssembly(name).HandwrittenTypes
            .Where(type => !module.Contains(type, "Contracts"))
            .Select(type => type.FullName);

        strayTypeNames.ShouldBeEmpty();
    }

    [Fact]
    public void Test_module_assembly_holds_only_test_module_namespaces() =>
        ArchModules.TestModulesAssembly.HandwrittenTypes
            .Where(type => !IsInTestModuleNamespace(type))
            .Select(type => type.FullName)
            .ShouldBeEmpty();

    private static Boolean IsInTestModuleNamespace(Type type) =>
        TestModuleNamespaces.Any(testModuleNamespace => type.IsInNamespace(testModuleNamespace));
}
