namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

internal static class RuleCases
{
    public static readonly IReadOnlyDictionary<String, RuleCase> All =
        ModuleRuleCases.All
            .Concat(ProjectRuleCases.All)
            .ToDictionary(ruleCase => ruleCase.Key, StringComparer.Ordinal);
}
