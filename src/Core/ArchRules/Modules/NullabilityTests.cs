using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class NullabilityTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Domain_and_feature_classes_return_optional_instead_of_null(String name) =>
        Check(
            NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull(GetModule(name)),
            $"{name} has no Domain or Features types",
            "Return Optional<T> for a value that may be absent");
}
