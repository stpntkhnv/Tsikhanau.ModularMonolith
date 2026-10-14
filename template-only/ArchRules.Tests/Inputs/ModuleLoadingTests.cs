using System.Reflection;
using Microsoft.CodeAnalysis;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Projects;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.ArchRules.Tests.Inputs;

public sealed class ModuleLoadingTests
{
    private const String GhostRootNamespace = "Tsikhanau.Monolith.Ghost";

    private const String GhostNotAdded =
        "Tsikhanau.Monolith.ArchRules.Tests does not add src/Modules/Ghost (Tsikhanau.Monolith.Ghost.GhostModule)";

    private const String GhostLoadFailure =
        $"Ghost cannot load its assemblies: Could not load file or assembly '{GhostRootNamespace},";

    private const String OrphanRootNamespace = "Tsikhanau.Monolith.Orphan";

    private const String OrphanBaseAssemblyName = "Tsikhanau.Monolith.OrphanBase";

    private const String OrphanLoadFailure =
        $"Orphan cannot load its assemblies: Could not load file or assembly '{OrphanBaseAssemblyName},";

    private const String OrphanSource =
        $"namespace {OrphanRootNamespace}.Domain;\n\npublic sealed class Orphan : {OrphanBaseAssemblyName}.OrphanBase;";

    private static readonly Lazy<Byte[]> OrphanAssemblyImage = new(CompileOrphanAssembly);

    private static ArchModule GhostModule =>
        new("Ghost", GhostRootNamespace, () => [Assembly.Load(GhostRootNamespace)]);

    private static ArchModule OrphanModule =>
        new("Orphan", OrphanRootNamespace, () => [Assembly.Load(OrphanAssemblyImage.Value)]);

    [Fact]
    public void Module_loads_its_assemblies_once_when_a_rule_first_asks_for_them()
    {
        var loadCount = 0;
        var pingsModule = new ArchModule(
            "Pings",
            "Tsikhanau.Monolith.TestModules.Pings",
            () =>
            {
                loadCount++;
                return [typeof(PingsModule).Assembly];
            });

        var loadCountBeforeRules = loadCount;
        _ = DomainRules.DomainTypesHaveNoPublicSetters(pingsModule);
        _ = NamingRules.TypesCarryTheSuffixOfTheirRole(pingsModule);

        loadCountBeforeRules.ShouldBe(0);
        loadCount.ShouldBe(1);
    }

    [Fact]
    public void Rules_find_nothing_to_check_in_a_module_whose_assemblies_cannot_load()
    {
        var evaluation = DomainRules.DomainTypesHaveNoPublicSetters(GhostModule);

        evaluation.Subject.ShouldBeEmpty();
    }

    [Fact]
    public void Coverage_rule_reports_a_module_whose_assemblies_cannot_load()
    {
        var evaluation = CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts(GhostModule);

        evaluation.Violations
            .ShouldHaveSingleItem()
            .ShouldStartWith(GhostLoadFailure);
    }

    [Fact]
    public void Rules_find_nothing_to_check_in_a_module_whose_types_cannot_load()
    {
        var evaluation = DomainRules.DomainTypesHaveNoPublicSetters(OrphanModule);

        evaluation.Subject.ShouldBeEmpty();
    }

    [Fact]
    public void Coverage_rule_reports_a_module_whose_types_cannot_load()
    {
        var evaluation = CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts(OrphanModule);

        evaluation.Violations
            .ShouldHaveSingleItem()
            .ShouldStartWith(OrphanLoadFailure);
    }

    [Fact]
    public void Host_rule_names_the_folder_of_a_module_it_does_not_add()
    {
        var evaluation = HostRules.HostAddsEveryModuleFolder(
            typeof(ModuleLoadingTests).Assembly,
            new ArchModules([GhostModule]));

        evaluation.Violations
            .ShouldHaveSingleItem()
            .ShouldBe(GhostNotAdded);
    }

    private static Byte[] CompileOrphanAssembly()
    {
        var baseImage = FixtureCompiler.Compile(
            OrphanBaseAssemblyName,
            $"namespace {OrphanBaseAssemblyName};\n\npublic class OrphanBase;",
            FixtureCompiler.RuntimeReferences);

        return FixtureCompiler.Compile(
            OrphanRootNamespace,
            OrphanSource,
            [.. FixtureCompiler.RuntimeReferences, MetadataReference.CreateFromImage(baseImage)]);
    }
}
