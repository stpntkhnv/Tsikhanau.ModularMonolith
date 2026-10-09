using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class SharedTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Shared_code_does_not_depend_on_the_rest_of_the_app() =>
        Check(SharedRules.SharedCodeDoesNotDependOnTheRestOfTheApp(Solution), "Tsikhanau.Monolith.Shared has no types");
}
