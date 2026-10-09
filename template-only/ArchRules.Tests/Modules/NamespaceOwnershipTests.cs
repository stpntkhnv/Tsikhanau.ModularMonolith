using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.ArchRules.Tests.Inputs;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class NamespaceOwnershipTests : NamespaceOwnershipTests<TestModuleInputs>
{
    private const String UnhandledFixtureNamespace = "Tsikhanau.Monolith.TestModules.Unhandled";

    private static readonly String[] TestModuleNamespaces =
    [
        .. Modules.All.Select(module => module.RootNamespace),
        UnhandledFixtureNamespace,
    ];

    [Fact]
    public void Test_module_assembly_holds_only_test_module_namespaces() =>
        typeof(PingsModule).Assembly.HandwrittenTypes
            .Where(type => !IsInTestModuleNamespace(type))
            .Select(type => type.FullName)
            .ShouldBeEmpty();

    private static Boolean IsInTestModuleNamespace(Type type) =>
        TestModuleNamespaces.Any(testModuleNamespace => type.IsInNamespace(testModuleNamespace));
}
