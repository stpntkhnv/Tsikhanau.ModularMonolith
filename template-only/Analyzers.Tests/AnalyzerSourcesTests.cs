using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class AnalyzerSourcesTests
{
    private const String NoRepositoryRoot = "no folder above the test output holds global.json";

    private static readonly String RepositoryRoot = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory));

    private static readonly String[] SourceFolders = ["src/Core/Analyzers", "template-only/Analyzers.Tests"];

    private static readonly ImmutableArray<DiagnosticAnalyzer> AnalyzersLeftOut = [new LineLengthAnalyzer()];

    [Fact]
    public async Task Analyzer_projects_pass_the_core_analyzers_their_build_leaves_out()
    {
        var problems = await FindProblemsAsync();

        problems.ShouldBeEmpty();
    }

    private static async Task<IReadOnlyList<String>> FindProblemsAsync()
    {
        var trees = await Task.WhenAll(SourceFiles.Select(ParseAsync));
        var diagnostics = await CSharpCompilation
            .Create("AnalyzerSources", trees)
            .WithAnalyzers(AnalyzersLeftOut)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        return diagnostics
            .Select(Describe)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<String> SourceFiles =>
        SourceFolders
            .Select(folder => Path.Combine(RepositoryRoot, folder))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file));

    private static Boolean IsBuildOutput(String file) =>
        Path
            .GetRelativePath(RepositoryRoot, file)
            .Split(Path.DirectorySeparatorChar)
            .Any(part => part is "bin" or "obj");

    private static async Task<SyntaxTree> ParseAsync(String file)
    {
        var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);

        return CSharpSyntaxTree.ParseText(
            text,
            path: Path.GetRelativePath(RepositoryRoot, file),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static String Describe(Diagnostic diagnostic)
    {
        var place = diagnostic.Location.GetLineSpan();
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);

        return $"{place.Path}:{place.StartLinePosition.Line + 1}: {diagnostic.Id} {message}";
    }

    private static String FindRepositoryRoot(DirectoryInfo folder) =>
        File.Exists(Path.Combine(folder.FullName, "global.json"))
            ? folder.FullName
            : FindRepositoryRoot(folder.Parent ?? throw new InvalidOperationException(NoRepositoryRoot));
}
