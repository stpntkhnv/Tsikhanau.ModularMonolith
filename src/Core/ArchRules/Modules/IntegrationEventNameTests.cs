using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class IntegrationEventNameTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Every_integration_event_has_a_unique_stable_name() =>
        Check(
            IntegrationEventNameRules.EveryIntegrationEventHasAUniqueStableName(Modules),
            "No module has integration events");
}
