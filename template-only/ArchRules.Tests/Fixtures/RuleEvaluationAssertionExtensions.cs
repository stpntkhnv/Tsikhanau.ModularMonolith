using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal static class RuleEvaluationAssertionExtensions
{
    private const String EmptySubject = "the fixture gives the rule nothing to check";

    extension(RuleEvaluation evaluation)
    {
        public void ShouldFindExactly(IReadOnlyList<String> violations)
        {
            evaluation.Subject.ShouldNotBeEmpty(EmptySubject);
            evaluation.Violations.ShouldBe(violations, ignoreOrder: true);
        }

        public void ShouldBeSilent()
        {
            evaluation.Subject.ShouldNotBeEmpty(EmptySubject);
            evaluation.Violations.ShouldBeEmpty();
        }
    }
}
