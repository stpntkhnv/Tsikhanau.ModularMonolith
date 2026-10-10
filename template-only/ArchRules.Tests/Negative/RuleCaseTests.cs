using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

public sealed class RuleCaseTests
{
    private const BindingFlags DeclaredPublicStaticMembers =
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static TheoryData<String> Rules => [.. RuleCases.All.Keys];

    [Theory]
    [MemberData(nameof(Rules))]
    public void Rule_finds_exactly_its_violations_in_the_bad_fixture(String rule)
    {
        var ruleCase = RuleCases.All[rule];

        var evaluation = ruleCase.EvaluateBad();

        evaluation.ShouldFindExactly(ruleCase.Violations);
    }

    [Theory]
    [MemberData(nameof(Rules))]
    public void Rule_is_silent_on_the_good_fixture(String rule)
    {
        var ruleCase = RuleCases.All[rule];

        var evaluation = ruleCase.EvaluateGood();

        evaluation.ShouldBeSilent();
    }

    [Fact]
    public void Every_module_rule_function_has_a_case_and_every_module_rule_case_a_function()
    {
        IReadOnlyList<String> ruleFunctions = [.. GetModuleRuleFunctions()];
        IReadOnlyList<String> ruleCases = [.. GetModuleRuleCases()];

        ruleFunctions
            .Except(ruleCases)
            .ShouldBeEmpty("module rule functions without a case");

        ruleCases
            .Except(ruleFunctions)
            .ShouldBeEmpty("module rule cases without a rule function");
    }

    private static IEnumerable<String> GetModuleRuleFunctions() =>
        typeof(RuleEvaluation).Assembly
            .GetExportedTypes()
            .Where(IsModuleRuleSet)
            .SelectMany(GetRuleFunctions);

    private static IEnumerable<String> GetRuleFunctions(Type ruleSetType) =>
        ruleSetType
            .GetMethods(DeclaredPublicStaticMembers)
            .Where(method => method.ReturnType == typeof(RuleEvaluation))
            .Select(method => $"{ruleSetType.Name}.{method.Name}");

    private static IEnumerable<String> GetModuleRuleCases() =>
        RuleCases.All.Values
            .Where(ruleCase => IsModuleRuleSet(ruleCase.RuleSetType))
            .Select(ruleCase => ruleCase.Key);

    private static Boolean IsModuleRuleSet(Type ruleSetType) =>
        ruleSetType.Namespace == typeof(DomainRules).Namespace;
}
