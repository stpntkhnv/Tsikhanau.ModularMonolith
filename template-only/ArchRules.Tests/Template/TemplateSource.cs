using System.Text.Json;
using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Template;

internal sealed partial record TemplateSource(
    IReadOnlyList<String> Include,
    IReadOnlyList<String> Exclude,
    IReadOnlyList<String> CopyOnly)
{
    private static readonly String TemplatePath = Path.Combine(SolutionPaths.Root, ".template.config", "template.json");

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    [GeneratedRegex(@"\*\*/|\*\*|\*|\?|\[[^\]]+\]|[^*?\[]+")]
    private static partial Regex GlobToken { get; }

    public static async Task<TemplateSource> ReadAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(TemplatePath);
        var template = await JsonSerializer.DeserializeAsync<TemplateFile>(
            stream,
            JsonSerializerOptions,
            cancellationToken);

        return template
            .ShouldNotBeNull()
            .Sources
            .ShouldHaveSingleItem();
    }

    public Boolean IsClassified(String path) => IsShipped(path) || IsExcluded(path);

    public Boolean IsShipped(String path) => Include.Any(glob => Matches(glob, path));

    public Boolean IsExcluded(String path) => Exclude.Any(glob => Matches(glob, path));

    private static Boolean Matches(String glob, String path) => Regex.IsMatch(path, BuildPattern(glob));

    private static String BuildPattern(String glob)
    {
        var patternParts = GlobToken
            .Matches(glob)
            .Select(token => BuildPatternPart(token.Value));

        return $"^{String.Concat(patternParts)}$";
    }

    private static String BuildPatternPart(String token) =>
        token switch
        {
            "**/" => "(?:.*/)?",
            "**" => ".*",
            "*" => "[^/]*",
            "?" => "[^/]",
            _ when token.StartsWith('[') => token,
            _ => Regex.Escape(token),
        };

    private sealed record TemplateFile(IReadOnlyList<TemplateSource> Sources);
}
