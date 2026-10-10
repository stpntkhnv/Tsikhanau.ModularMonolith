using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

internal sealed record RuleCase(
    Type RuleSetType,
    String Rule,
    Func<RuleEvaluation> EvaluateBad,
    IReadOnlyList<String> Violations,
    Func<RuleEvaluation> EvaluateGood)
{
    public String Key => $"{RuleSetType.Name}.{Rule}";
}
