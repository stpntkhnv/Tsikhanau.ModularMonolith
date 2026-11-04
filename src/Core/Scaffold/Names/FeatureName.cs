using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Names;

internal static class FeatureName
{
    private const String AddedSuffixRule = "scaffold adds the suffix of each type itself";

    private static readonly String[] AddedSuffixes =
        ["Command", "Handler", "Request", "Endpoint", "Tests", "Query", "Dto", "Event"];

    public static Result<String> Validate(String name) =>
        Result
            .Success(name)
            .Ensure(PascalCase.Matches, BuildError(name, PascalCase.Rule))
            .Bind(EnsureNoAddedSuffix);

    private static Result<String> EnsureNoAddedSuffix(String name) =>
        FindAddedSuffix(name)
            .Match(
                suffix => BuildError(name, $"must not end in {suffix}: {AddedSuffixRule}"),
                () => Result.Success(name));

    private static Optional<String> FindAddedSuffix(String name) =>
        AddedSuffixes.FirstOrDefault(suffix => name.EndsWith(suffix, StringComparison.Ordinal));

    private static Error BuildError(String name, String rule) =>
        Error.Failure("scaffold.name", $"feature name '{name}' {rule}");
}
