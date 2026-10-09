using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class NamingTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Types_carry_the_suffix_of_their_role(String name) =>
        Check(NamingRules.TypesCarryTheSuffixOfTheirRole(GetModule(name)), $"{name} has no concrete types");
}
