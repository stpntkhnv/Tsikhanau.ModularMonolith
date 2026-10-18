using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class SuppressionRules
{
    private const String MigrationsFolderName = "Migrations";

    public static RuleEvaluation CodeSuppressesNoDiagnostic(ArchSolution solution)
    {
        IReadOnlyList<String> sourcePaths = [.. GetSourcePaths(solution)];
        IReadOnlyList<String> subject = [.. sourcePaths.Select(solution.ToSolutionPath)];

        return RuleEvaluation.Create(subject, () => sourcePaths.SelectMany(path => GetViolations(solution, path)));
    }

    private static IEnumerable<String> GetSourcePaths(ArchSolution solution) =>
        BuildFileRules
            .GetFoldersThatAffectCompilation(solution)
            .SelectMany(folder => Directory.EnumerateFiles(folder.Path, "*.cs"))
            .Order(StringComparer.Ordinal);

    private static IEnumerable<String> GetViolations(ArchSolution solution, String sourcePath)
    {
        var solutionPath = solution.ToSolutionPath(sourcePath);

        return CollectViolations(sourcePath)
            .OrderBy(finding => finding.Line)
            .Select(finding => $"{solutionPath}:{finding.Line} {finding.Text}");
    }

    private static IEnumerable<SourceFinding> CollectViolations(String sourcePath)
    {
        var source = ParseSource(sourcePath);
        var fileName = Path.GetFileName(sourcePath);

        return IsInMigrationsFolder(sourcePath)
            ? CollectViolationsUnderMigrations(source, fileName)
            : [.. GeneratedFileNames.Collect(fileName), .. source.CollectSuppressions()];
    }

    private static IEnumerable<SourceFinding> CollectViolationsUnderMigrations(
        CompilationUnitSyntax source,
        String fileName) =>
        [.. GeneratedFileNames.CollectExceptDotnetEfDesigner(fileName), .. source.CollectWhatDotnetEfDoesNotWrite()];

    private static CompilationUnitSyntax ParseSource(String sourcePath) =>
        CSharpSyntaxTree
            .ParseText(File.ReadAllText(sourcePath))
            .GetCompilationUnitRoot();

    private static Boolean IsInMigrationsFolder(String sourcePath) =>
        Path.GetFileName(Path.GetDirectoryName(sourcePath)) == MigrationsFolderName;
}
