using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Template;

public sealed partial class CoreFilesTests
{
    private const String AppNamePattern = "modular.?monolith";

    [GeneratedRegex(AppNamePattern, RegexOptions.IgnoreCase)]
    private static partial Regex AppName { get; }

    [Fact]
    public async Task Core_files_do_not_name_the_app()
    {
        var coreFiles = await ListCoreFilesAsync();

        var appNames = coreFiles.SelectMany(FindAppName);

        coreFiles.ShouldNotBeEmpty();
        appNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Core_files_check_out_with_lf_line_endings()
    {
        var coreFiles = await ListCoreFilesAsync();

        var attributeLines = await ReadLineEndingAttributesAsync(coreFiles);
        var filesWithoutLf = FindFilesWithoutLf(attributeLines);

        coreFiles.ShouldNotBeEmpty();
        filesWithoutLf.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<String>> ListCoreFilesAsync() =>
        RepositoryCommands.RunAsync("bash", ["scripts/core-hash.sh", "list"], []);

    private static Task<IReadOnlyList<String>> ReadLineEndingAttributesAsync(IReadOnlyList<String> paths) =>
        RepositoryCommands.RunAsync(
            "git",
            ["-c", "core.attributesFile=/dev/null", "check-attr", "--stdin", "text", "eol"],
            paths);

    private static IEnumerable<String> FindAppName(String path)
    {
        var lines = File
            .ReadLines(Path.Combine(SolutionPaths.Root, path))
            .Select((text, index) => (Text: text, Number: index + 1))
            .Where(line => NamesTheApp(line.Text))
            .Select(line => $"{path}:{line.Number}: {line.Text.Trim()}");

        return NamesTheApp(path) ? lines.Prepend(path) : lines;
    }

    private static Boolean NamesTheApp(String text) => AppName.IsMatch(text);

    private static IEnumerable<String> FindFilesWithoutLf(IReadOnlyList<String> attributeLines) =>
        attributeLines
            .Select(line => line.Split(": "))
            .GroupBy(parts => parts[0], parts => $"{parts[1]}: {parts[2]}")
            .Where(attributes => !KeepsLf([.. attributes]))
            .Select(attributes => $"{attributes.Key} ({String.Join(", ", attributes)})");

    private static Boolean KeepsLf(IReadOnlyList<String> attributes) =>
        attributes.Contains("text: unset") || (attributes.Contains("text: set") && attributes.Contains("eol: lf"));
}
