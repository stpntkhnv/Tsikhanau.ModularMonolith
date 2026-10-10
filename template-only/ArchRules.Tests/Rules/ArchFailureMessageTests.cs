using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

namespace Tsikhanau.Monolith.ArchRules.Tests.Rules;

public sealed class ArchFailureMessageTests : ArchRuleTests<FixtureInputs>
{
    private const String OptionalFix = "Return Optional<T> for a value that may be absent";

    private const String OptionalRuleLine =
        "Rule in AGENTS.md: Absence is `Optional<T>`, never `null`: in what Domain and Features return and in what "
        + "messages and requests carry.";

    private const String UncheckedFactoryFix =
        "Make the unchecked From factory private; callers go through the factory that returns Result";

    [Fact]
    public void Failing_rule_states_the_fix_the_rule_line_and_the_playbook()
    {
        var failure = Should.Throw<ShouldAssertException>(Domain_and_feature_classes_return_optional_instead_of_null);

        failure.Message.ShouldContain("Acme.Modules.Bad.Domain.Notes.NoteLookup.Find");
        failure.Message.ShouldContain($"Fix: {OptionalFix}");
        failure.Message.ShouldContain(OptionalRuleLine);
        failure.Message.ShouldContain("Playbook: docs/playbooks/add-feature.md");
    }

    [Fact]
    public void Failing_rule_that_agents_does_not_state_gives_only_the_fix()
    {
        var failure = Should.Throw<ShouldAssertException>(Value_objects_hide_the_unchecked_factory);

        failure.Message.ShouldContain($"Fix: {UncheckedFactoryFix}");
        failure.Message.ShouldNotContain("AGENTS.md");
        failure.Message.ShouldNotContain("Playbook");
    }

    private static void Domain_and_feature_classes_return_optional_instead_of_null() =>
        Check(
            NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull(FixtureModules.Bad),
            "Bad has no Domain or Features types",
            OptionalFix);

    private static void Value_objects_hide_the_unchecked_factory() =>
        Check(
            DomainRules.ValueObjectsHideTheUncheckedFactory(FixtureModules.Bad),
            "Bad has no value objects",
            UncheckedFactoryFix);
}
