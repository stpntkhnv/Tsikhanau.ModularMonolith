using System.Text.RegularExpressions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed partial record EndpointRoute
{
    private const String SampleId = "017f22e2-79b0-7cc3-98c4-dc0c0c07398f";

    private const String StartRule = "must be empty, the collection of the module, or start with /";

    private const String EndRule = "must not end in /";

    private const String EmptySegmentRule = "has an empty segment between two /";

    private const String SegmentRule = "a segment is kebab-case words (archive, note-tags) or an id {name:guid}";

    private const String GuidIdRule = "an id is a Guid v7, so its parameter is {name:guid}";

    private EndpointRoute(String path) => Path = path;

    public String Path { get; }

    public Boolean IsCollection => Path.Length == 0;

    public String PathWithSampleIds => ParameterPattern.Replace(Path, SampleId);

    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex KebabCaseWordsPattern { get; }

    [GeneratedRegex(@"\A\{[a-z][A-Za-z0-9]*:guid\}\z")]
    private static partial Regex GuidIdPattern { get; }

    [GeneratedRegex(@"\A\{[a-z][A-Za-z0-9]*(:[^{}]*)?\}\z")]
    private static partial Regex ParameterSegmentPattern { get; }

    [GeneratedRegex(@"\{(?<name>[^{}:]*)(?<constraint>[^{}]*)\}")]
    private static partial Regex ParameterPattern { get; }

    public static EndpointRoute From(String path) => new(path);

    public static Result<EndpointRoute> Parse(String path) =>
        Result
            .Success(path)
            .Ensure(StartsAsRoute, BuildError(path, StartRule))
            .Ensure(HasNoTrailingSlash, BuildError(path, EndRule))
            .Bind(EnsureSegmentsAreWordsOrGuidIds)
            .Bind(EnsureParameterNamesDiffer)
            .Map(validPath => new EndpointRoute(validPath));

    public Boolean Matches(String mappedPath) => RemoveParameterNames(mappedPath) == RemoveParameterNames(Path);

    private static Boolean StartsAsRoute(String path) => path.Length == 0 || path.StartsWith('/');

    private static Boolean HasNoTrailingSlash(String path) => !path.EndsWith('/');

    private static Result<String> EnsureSegmentsAreWordsOrGuidIds(String path) =>
        FindBadSegment(path)
            .Match(
                segment => BuildError(path, DescribeBadSegment(segment)),
                () => Result.Success(path));

    private static Optional<String> FindBadSegment(String path) =>
        GetSegments(path)
            .FirstOrDefault(segment => !IsWordsOrGuidId(segment));

    private static Boolean IsWordsOrGuidId(String segment) =>
        KebabCaseWordsPattern.IsMatch(segment) || GuidIdPattern.IsMatch(segment);

    private static String DescribeBadSegment(String segment) =>
        segment switch
        {
            "" => EmptySegmentRule,
            _ when ParameterSegmentPattern.IsMatch(segment) => $"has the parameter {segment}: {GuidIdRule}",
            _ => $"has the segment '{segment}': {SegmentRule}",
        };

    private static Result<String> EnsureParameterNamesDiffer(String path) =>
        FindRepeatedParameterName(path)
            .Match(
                name => BuildError(path, $"names the parameter {name} twice: each parameter needs its own name"),
                () => Result.Success(path));

    private static Optional<String> FindRepeatedParameterName(String path) =>
        ParameterPattern
            .Matches(path)
            .CountBy(parameter => parameter.Groups["name"].Value)
            .Where(nameCount => nameCount.Value > 1)
            .Select(nameCount => nameCount.Key)
            .FirstOrDefault();

    private static IEnumerable<String> GetSegments(String path) =>
        path
            .Split('/')
            .Skip(1);

    private static String RemoveParameterNames(String path) => ParameterPattern.Replace(path, "{${constraint}}");

    private static Error BuildError(String path, String rule) =>
        Error.Failure("scaffold.route", $"route '{path}' {rule}");
}
