using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Rules;

public sealed class ArchFailureMessageTests : ArchRuleTests<FixtureInputs>
{
    private const String OptionalFix = "Return Optional<T> for a value that may be absent";

    private const String OptionalRuleLine =
        "Rule in AGENTS.md: Absence is `Optional<T>`, never `null`: in what Domain types and feature classes return "
        + "and in what integration events, commands and request records carry. Queries and DTOs carry `Optional<T>` "
        + "too; only a Dapper `<Name>Row` record holds a nullable member, which its handler maps to the DTO at once.";

    private const String UncheckedFactoryFix =
        "Implement the unchecked From explicitly as IValueObject<TSelf, TValue>.From; callers go through the factory "
        + "that returns Result";

    private const String SettingsFix =
        "Bind a settings record with AddSettings<TSettings>, take IOptions<TSettings> in the constructor and read its "
        + "Value";

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
        var failure = Should.Throw<ShouldAssertException>(
            () => new BadDomainRules().Value_objects_hide_the_unchecked_factory(FixtureModules.Bad.Name));

        failure.Message.ShouldContain($"Fix: {UncheckedFactoryFix}");
        failure.Message.ShouldNotContain("AGENTS.md");
        failure.Message.ShouldNotContain("Playbook");
    }

    [Fact]
    public void Configuration_failure_names_the_options_the_settings_come_through()
    {
        var failure = Should.Throw<ShouldAssertException>(
            () => new BadLayerDependencies().Module_does_not_read_configuration_directly(FixtureModules.Bad.Name));

        failure.Message.ShouldContain($"Fix: {SettingsFix}");
        failure.Message.ShouldContain("Playbook: docs/playbooks/add-settings.md");
    }

    private static void Domain_and_feature_classes_return_optional_instead_of_null() =>
        Check(
            NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull(FixtureModules.Bad),
            "Bad has no Domain or Features types",
            OptionalFix);

    private sealed class BadInputs : IArchRuleInputs
    {
        public static IReadOnlyList<ArchModule> Modules =>
            [FixtureModules.Good, FixtureModules.Peer, FixtureModules.Bad];

        public static String RootNamespace => FixtureModules.RootNamespace;

        public static String SolutionRoot => SolutionPaths.Root;

        public static EmptySubject EmptySubject => EmptySubject.Pass;

        public static IReadOnlyList<Assembly> SharedKernelAssemblies => [FixtureModules.Kernel];
    }

    private sealed class BadDomainRules : DomainRulesTests<BadInputs>;

    private sealed class BadLayerDependencies : LayerDependencyTests<BadInputs>;
}
