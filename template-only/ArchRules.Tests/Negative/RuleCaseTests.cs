using System.Reflection;
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
    public void Every_rule_function_has_a_case_and_every_case_a_rule_function()
    {
        IReadOnlyList<String> ruleFunctions = [.. GetRuleFunctions()];
        IReadOnlyList<String> ruleCases = [.. RuleCases.All.Keys];

        ruleFunctions
            .Except(ruleCases)
            .ShouldBeEmpty("rule functions without a case");

        ruleCases
            .Except(ruleFunctions)
            .ShouldBeEmpty("cases without a rule function");
    }

    private static IEnumerable<String> GetRuleFunctions() =>
        typeof(RuleEvaluation).Assembly
            .GetExportedTypes()
            .SelectMany(GetRuleFunctions);

    private static IEnumerable<String> GetRuleFunctions(Type ruleSetType) =>
        ruleSetType
            .GetMethods(DeclaredPublicStaticMembers)
            .Where(method => method.ReturnType == typeof(RuleEvaluation))
            .Select(method => $"{ruleSetType.Name}.{method.Name}");
}
