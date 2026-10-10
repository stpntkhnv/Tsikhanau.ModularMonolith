using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class HostProjectEdit
{
    [GeneratedRegex("^\\s*<ProjectReference Include=\"(?<include>[^\"]*)\"\\s*/>\\s*$")]
    private static partial Regex ProjectReferencePattern { get; }

    public static Result<FileEdit> AddModule(TextFile hostProject, ScaffoldedModule module) =>
        hostProject.PlanEdit(text =>
            AddReference(TextLines.Split(text, hostProject.NewLine), hostProject.Path, module));

    private static Result<String> AddReference(TextLines lines, String path, ScaffoldedModule module)
    {
        var include = $@"..\..\Modules\{module.Name}\{module.ProjectName}\{module.ProjectName}.csproj";
        var referenceIndexes = lines.FindIndexes(ProjectReferencePattern.IsMatch);

        return Result
            .Success(lines)
            .Ensure(_ => referenceIndexes.Count > 0, BuildAnchorError(path, "a ProjectReference to the core"))
            .Ensure(
                _ => !referenceIndexes.Any(index => Includes(lines.Lines[index], include)),
                BuildAnchorError(path, $"no reference to {include} yet"))
            .Map(_ => lines.Insert(
                FindInsertionIndex(lines, referenceIndexes, include),
                BuildReference(lines.Lines[referenceIndexes[^1]], include)))
            .Map(updatedLines => updatedLines.Join());
    }

    private static Int32 FindInsertionIndex(TextLines lines, IReadOnlyList<Int32> referenceIndexes, String include) =>
        referenceIndexes
            .Where(index => String.CompareOrdinal(ReadInclude(lines.Lines[index]), include) > 0)
            .DefaultIfEmpty(referenceIndexes[^1] + 1)
            .First();

    private static String BuildReference(String neighbour, String include) =>
        $"{TextLines.ReadIndentation(neighbour)}<ProjectReference Include=\"{include}\" />";

    private static Boolean Includes(String line, String include) =>
        String.Equals(ReadInclude(line), include, StringComparison.OrdinalIgnoreCase);

    private static String ReadInclude(String line) => ProjectReferencePattern.Match(line).Groups["include"].Value;

    private static Error BuildAnchorError(String path, String expected) =>
        Error.Failure("scaffold.anchor", $"{path} must hold {expected}");
}
