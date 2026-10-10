using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Projects;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.FixtureModules;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.ScratchTree;

namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

internal static class ProjectRuleCases
{
    private const String GhostContracts =
        "src/Modules/Ghost/Acme.Modules.Ghost.Contracts/Acme.Modules.Ghost.Contracts.csproj";

    private const String StrayProject = "Acme.Modules.Good.Tests/Acme.Modules.Good.Tests.csproj";

    private const String NestedBuildFile = "src/Modules/Good/Directory.Build.props";

    private const String SharedFixtureName = "Tsikhanau.Monolith.Shared.Fixture";

    private const String IntegrationEventTypeName = "Tsikhanau.Monolith.Contracts.IntegrationEvents.IntegrationEvent";

    public static IEnumerable<RuleCase> All =>
    [
        new(
            typeof(HostRules),
            nameof(HostRules.HostAddsEveryModuleFolder),
            () => HostRules.HostAddsEveryModuleFolder(Host, Dirty),
            ["Acme.Bootstrapper does not add src/Modules/Bad (Acme.Modules.Bad.BadModule)"],
            () => HostRules.HostAddsEveryModuleFolder(Host, Clean)),
        new(
            typeof(ModuleGraphRules),
            nameof(ModuleGraphRules.ModulesDoNotReferenceEachOtherInACycle),
            () => Evaluate(
                tree => tree.AddReference(PeerProject, GoodContracts),
                solution => ModuleGraphRules.ModulesDoNotReferenceEachOtherInACycle(solution, Clean)),
            ["Good -> Peer -> Good"],
            () => Evaluate(solution => ModuleGraphRules.ModulesDoNotReferenceEachOtherInACycle(solution, Clean))),
        new(
            typeof(ModuleGraphRules),
            nameof(ModuleGraphRules.ModulesReferenceOnlyContractsOfKnownModules),
            () => Evaluate(
                tree => tree.AddReference(GoodProject, GhostContracts),
                solution => ModuleGraphRules.ModulesReferenceOnlyContractsOfKnownModules(solution, Clean)),
            ["Good references Acme.Modules.Ghost.Contracts"],
            () => Evaluate(solution => ModuleGraphRules.ModulesReferenceOnlyContractsOfKnownModules(solution, Clean))),
        new(
            typeof(ProjectGraphRules),
            nameof(ProjectGraphRules.ModuleHasItsProjectAndItsContracts),
            () => Evaluate(
                tree => tree.AddProject($"src/Modules/Good/{StrayProject}"),
                solution => ProjectGraphRules.ModuleHasItsProjectAndItsContracts(solution, Good)),
            [$"Good has {StrayProject}, which is neither its project nor its contracts"],
            () => Evaluate(solution => ProjectGraphRules.ModuleHasItsProjectAndItsContracts(solution, Good))),
        new(
            typeof(ProjectGraphRules),
            nameof(ProjectGraphRules.ProjectReferencesFollowTheLayerRules),
            () => Evaluate(
                tree => tree.AddReference(GoodContracts, CoreDomain),
                solution => ProjectGraphRules.ProjectReferencesFollowTheLayerRules(solution, Clean, Good)),
            ["Acme.Modules.Good.Contracts references Tsikhanau.Monolith.Domain"],
            () => Evaluate(solution => ProjectGraphRules.ProjectReferencesFollowTheLayerRules(solution, Clean, Good))),
        new(
            typeof(CoreRules),
            nameof(CoreRules.CoreProjectsReferenceOnlyCoreAndPackageProjects),
            () => Evaluate(
                tree => tree.AddReference(CoreDomain, GoodContracts),
                CoreRules.CoreProjectsReferenceOnlyCoreAndPackageProjects),
            [$"{CoreDomain} references {GoodContracts}"],
            () => Evaluate(CoreRules.CoreProjectsReferenceOnlyCoreAndPackageProjects)),
        new(
            typeof(BuildFileRules),
            nameof(BuildFileRules.OnlyTheCoreHasBuildAndAnalyzerConfigFiles),
            () => Evaluate(
                tree => tree.AddFile(NestedBuildFile, "<Project />"),
                BuildFileRules.OnlyTheCoreHasBuildAndAnalyzerConfigFiles),
            [NestedBuildFile],
            () => Evaluate(BuildFileRules.OnlyTheCoreHasBuildAndAnalyzerConfigFiles)),
        new(
            typeof(PackageVersionRules),
            nameof(PackageVersionRules.AspireSdkAndAspirePackagesShareOneVersion),
            () => Evaluate(
                tree => tree.AddPackageVersion(AppPackages, "Aspire.Hosting.Redis", "12.0.0"),
                PackageVersionRules.AspireSdkAndAspirePackagesShareOneVersion),
            [$"Aspire.Hosting.Redis 12.0.0 differs from the AppHost Sdk {AspireVersion}"],
            () => Evaluate(PackageVersionRules.AspireSdkAndAspirePackagesShareOneVersion)),
        new(
            typeof(SharedRules),
            nameof(SharedRules.SharedCodeDoesNotDependOnTheRestOfTheApp),
            () => Evaluate(solution => SharedRules.SharedCodeDoesNotDependOnTheRestOfTheApp(solution, SharedFixture)),
            [$"{SharedFixtureName}.EventNames does depend on \"{IntegrationEventTypeName}\""],
            () => Evaluate(SharedRules.SharedCodeDoesNotDependOnTheRestOfTheApp)),
    ];

    private static Assembly SharedFixture => FixtureAssemblies.Get(SharedFixtureName);
}
