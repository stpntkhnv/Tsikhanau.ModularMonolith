using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class SolutionEdit
{
    private const String DefaultIndentation = "  ";

    [GeneratedRegex("^\\s*<Folder Name=\"(?<name>[^\"]*)\"")]
    private static partial Regex FolderPattern { get; }

    public static Result<FileEdit> AddModule(TextFile solution, ScaffoldedModule module) =>
        solution.PlanEdit(text => AddFolder(TextLines.Split(text, solution.NewLine), solution.Path, module));

    private static Result<String> AddFolder(TextLines lines, String path, ScaffoldedModule module)
    {
        var folderName = $"/{module.Folder}/";
        var folderIndexes = lines.GetIndexes(FolderPattern.IsMatch);
        var solutionEnds = lines.GetIndexes(line => line.Trim() == "</Solution>");

        return Result
            .Success(lines)
            .Ensure(_ => solutionEnds.Count == 1, BuildAnchorError(path, "one </Solution> line"))
            .Ensure(
                _ => !folderIndexes.Any(index => HasName(lines.Lines[index], folderName)),
                BuildAnchorError(path, $"no folder {folderName} yet"))
            .Map(_ => lines.Insert(
                GetInsertionIndex(lines, folderIndexes, folderName, solutionEnds[0]),
                BuildFolder(lines, folderIndexes, folderName, module)))
            .Map(updatedLines => updatedLines.Join());
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
        ScaffoldedModule module)
    {
        var indentation = folderIndexes.Count > 0
            ? TextLines.ReadIndentation(lines.Lines[folderIndexes[0]])
            : DefaultIndentation;

        String[] projectPaths = [module.ContractsProjectPath, module.ProjectPath];

        return
        [
            $"{indentation}<Folder Name=\"{folderName}\">",
            .. projectPaths
                .Order(StringComparer.Ordinal)
                .Select(projectPath => $"{indentation}{indentation}<Project Path=\"{projectPath}\" />"),
            $"{indentation}</Folder>",
        ];
    }

    private static Boolean HasName(String line, String folderName) =>
        String.Equals(ReadName(line), folderName, StringComparison.OrdinalIgnoreCase);

    private static String ReadName(String line) => FolderPattern.Match(line).Groups["name"].Value;

    private static Error BuildAnchorError(String path, String expected) =>
        Error.Failure("scaffold.anchor", $"{path} must hold {expected}");
}
