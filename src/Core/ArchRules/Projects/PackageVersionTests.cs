using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class PackageVersionTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Aspire_sdk_and_aspire_packages_share_one_version() =>
        Check(
            PackageVersionRules.AspireSdkAndAspirePackagesShareOneVersion(Solution),
            "Directory.Packages.props has no Aspire packages",
            "Bump the Aspire SDK in the AppHost Sdk attribute together with the Aspire packages");
}
