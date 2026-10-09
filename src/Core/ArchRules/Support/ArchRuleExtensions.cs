using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ArchRuleExtensions
{
    extension(IArchRule rule)
    {
        public IReadOnlyList<String> GetViolationsIn(Architecture architecture) =>
            rule
                .Evaluate(architecture)
                .Where(result => !result.Passed)
                .Select(result => result.Description)
                .ToList();
    }
}
