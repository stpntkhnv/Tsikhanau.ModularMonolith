using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class RuleEvaluationExtensions
{
    extension(IEnumerable<Type> subjectTypes)
    {
        public RuleEvaluation CollectViolations(Func<Type, IEnumerable<String>> describeViolations)
        {
            IReadOnlyList<Type> types = [.. subjectTypes];
            return RuleEvaluation.Create(types, () => types.SelectMany(describeViolations));
        }

        public RuleEvaluation CollectViolations(Func<Type, Boolean> isViolation)
        {
            IReadOnlyList<Type> types = [.. subjectTypes];
            return RuleEvaluation.Create(types, () => GetNames(types.Where(isViolation)));
        }
    }

    private static IEnumerable<String> GetNames(IEnumerable<Type> types) => types.Select(type => type.FullNameOrName);
}
