using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class CoreTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    private const String BuildFileAdvice =
        "MSBuild and the compiler read the nearest of these files, so a copy outside the core hides the core's "
        + "build and analyzer rules or turns them off. Put the app's own build settings in its project files and "
        + "its packages in App.Packages.props; a change to the core's settings is made in the template.";

    [Fact]
    public void Core_projects_reference_only_core_and_package_projects() =>
        Check(CoreRules.CoreProjectsReferenceOnlyCoreAndPackageProjects(Solution), "src/Core has no projects");

    [Fact]
    public void Only_the_core_has_build_and_analyzer_config_files() =>
        Check(
            BuildFileRules.OnlyTheCoreHasBuildAndAnalyzerConfigFiles(Solution),
            "The repository has no Directory.Build.props, .editorconfig or other build file",
            BuildFileAdvice);
}
