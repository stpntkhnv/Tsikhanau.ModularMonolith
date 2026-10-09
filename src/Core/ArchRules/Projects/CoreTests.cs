using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class CoreTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Core_projects_reference_only_core_and_package_projects() =>
        Check(CoreRules.CoreProjectsReferenceOnlyCoreAndPackageProjects(Solution), "src/Core has no projects");
}
