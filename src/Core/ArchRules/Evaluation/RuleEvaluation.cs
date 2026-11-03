using Tsikhanau.Monolith.ArchRules.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Evaluation;

public sealed record RuleEvaluation(IReadOnlyList<String> Subject, IReadOnlyList<String> Violations)
{
    internal static RuleEvaluation Create(IReadOnlyList<String> subject, Func<IEnumerable<String>> collectViolations) =>
        new(subject, subject.Count == 0 ? [] : [.. collectViolations()]);

    internal static RuleEvaluation Create(IReadOnlyList<Type> subjectTypes, Func<IEnumerable<String>> collectViolations)
    {
        IReadOnlyList<String> subject = [.. subjectTypes.Select(type => type.FullNameOrName)];
        return Create(subject, collectViolations);
    }
}
