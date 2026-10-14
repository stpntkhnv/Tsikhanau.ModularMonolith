using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.Testing.Modules;
using Xunit.Sdk;

namespace Tsikhanau.Monolith.ArchRules.Tests.Rules;

public sealed class EmptySubjectTests
{
    private const String NoEventReason = "No module has integration events";

    private const String NoModuleReason = "There are no production modules";

    [Fact]
    public void App_rule_with_nothing_to_check_skips_although_the_app_declares_pass()
    {
        var skip = Should.Throw<SkipException>(
            new LegacyAppEventNames().Every_integration_event_has_a_unique_stable_name);

        skip.Message.ShouldContain(NoEventReason);
    }

    [Fact]
    public void App_without_modules_gives_one_skipped_module_row()
    {
        var row = LegacyAppEventNames.ModuleNames.ShouldHaveSingleItem();

        row.Data.ShouldBe(ProductionModules.Placeholder);
        row.Skip.ShouldBe(NoModuleReason);
    }

    [Fact]
    public void Fixture_rule_with_nothing_to_check_passes_when_the_fixture_declares_pass() =>
        Should.NotThrow(new PassingFixtureEventNames().Every_integration_event_has_a_unique_stable_name);

    [Fact]
    public void Rule_with_nothing_to_check_skips_when_the_inputs_declare_no_choice()
    {
        var skip = Should.Throw<SkipException>(
            new UndeclaredEventNames().Every_integration_event_has_a_unique_stable_name);

        skip.Message.ShouldContain(NoEventReason);
    }

    private sealed class LegacyAppInputs : IAppArchRuleInputs
    {
        public static IReadOnlyList<ArchModule> Modules => [];

        public static String RootNamespace => "Acme";

        public static String SolutionRoot => SolutionPaths.Root;

        public static Assembly HostAssembly => typeof(LegacyAppInputs).Assembly;

        public static EmptySubject EmptySubject => EmptySubject.Pass;
    }

    private sealed class PassingFixtureInputs : IArchRuleInputs
    {
        public static IReadOnlyList<ArchModule> Modules => [];

        public static String RootNamespace => "Acme";

        public static String SolutionRoot => SolutionPaths.Root;

        public static EmptySubject EmptySubject => EmptySubject.Pass;
    }

    private sealed class UndeclaredInputs : IArchRuleInputs
    {
        public static IReadOnlyList<ArchModule> Modules => [];

        public static String RootNamespace => "Acme";

        public static String SolutionRoot => SolutionPaths.Root;
    }

    private sealed class LegacyAppEventNames : IntegrationEventNameTests<LegacyAppInputs>;

    private sealed class PassingFixtureEventNames : IntegrationEventNameTests<PassingFixtureInputs>;

    private sealed class UndeclaredEventNames : IntegrationEventNameTests<UndeclaredInputs>;
}
