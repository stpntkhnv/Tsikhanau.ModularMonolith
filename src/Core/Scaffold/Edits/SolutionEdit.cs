using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class SolutionEdit
{
    private const String DefaultIndentation = "  ";

    [GeneratedRegex("^\\s*<Folder Name=\"(?<name>[^\"]*)\"")]
    private static partial Regex FolderPattern { get; }

    public static Result<FileEdit> AddModule(TextFile solution, ScaffoldedModule module) =>
        solution.PlanLineEdit(lines => AddFolder(lines, solution, module));

    private static Result<TextLines> AddFolder(TextLines lines, TextFile solution, ScaffoldedModule module)
    {
        var folderName = $"/{module.Projects.Folder}/";
        var folderIndexes = lines.GetIndexes(FolderPattern.IsMatch);
        var solutionEnds = lines.GetIndexes(line => line.Trim() == "</Solution>");

        return Result
            .Success(lines)
            .Ensure(_ => solutionEnds.Count == 1, solution.MissingAnchor("one </Solution> line"))
            .Ensure(
                _ => !folderIndexes.Any(index => HasName(lines.Lines[index], folderName)),
                solution.MissingAnchor($"no folder {folderName} yet"))
            .Map(_ => lines.Insert(
                GetInsertionIndex(lines, folderIndexes, folderName, solutionEnds[0]),
                BuildFolder(lines, folderIndexes, folderName, module.Projects)));
    }

    private static Int32 GetInsertionIndex(
        TextLines lines,
        IReadOnlyList<Int32> folderIndexes,
        String folderName,
        Int32 solutionEnd) =>
        folderIndexes
            .Where(index => CodeLayout.NameOrder.Compare(ReadName(lines.Lines[index]), folderName) > 0)
            .DefaultIfEmpty(solutionEnd)
            .First();

    private static IEnumerable<String> BuildFolder(
        TextLines lines,
        IReadOnlyList<Int32> folderIndexes,
        String folderName,
        ModuleProjects projects)
    {
        var indentation = folderIndexes.Count > 0
            ? TextLines.ReadIndentation(lines.Lines[folderIndexes[0]])
            : DefaultIndentation;

        return
        [
            $"{indentation}<Folder Name=\"{folderName}\">",
            .. projects.ProjectPaths
                .Order(StringComparer.Ordinal)
                .Select(projectPath => $"{indentation}{indentation}<Project Path=\"{projectPath}\" />"),
            $"{indentation}</Folder>",
        ];
    }

    private static Boolean HasName(String line, String folderName) =>
        String.Equals(ReadName(line), folderName, StringComparison.OrdinalIgnoreCase);

    private static String ReadName(String line) => FolderPattern.Match(line).Groups["name"].Value;
}
