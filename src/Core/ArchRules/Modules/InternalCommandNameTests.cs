using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class InternalCommandNameTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Every_internal_command_has_a_stable_name_unique_in_its_module() =>
        Check(
            InternalCommandNameRules.EveryInternalCommandHasAStableNameUniqueInItsModule(Modules),
            "No module has internal commands");
}
