using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class HostProjectEdit
{
    [GeneratedRegex("^\\s*<ProjectReference Include=\"(?<include>[^\"]*)\"\\s*/>\\s*$")]
    private static partial Regex ProjectReferencePattern { get; }

    public static Result<FileEdit> AddModule(TextFile hostProject, ScaffoldedModule module) =>
        hostProject.PlanLineEdit(lines => AddReference(lines, hostProject, module));

    private static Result<TextLines> AddReference(TextLines lines, TextFile hostProject, ScaffoldedModule module)
    {
        var include = module.Projects.ProjectIncludeFromHost;
        var referenceIndexes = lines.GetIndexes(ProjectReferencePattern.IsMatch);

        return Result
            .Success(lines)
            .Ensure(_ => referenceIndexes.Count > 0, hostProject.MissingAnchor("a ProjectReference to the core"))
            .Ensure(
                _ => !referenceIndexes.Any(index => Includes(lines.Lines[index], include)),
                hostProject.MissingAnchor($"no reference to {include} yet"))
            .Map(_ => lines.Insert(
                GetInsertionIndex(lines, referenceIndexes, include),
                BuildReference(lines.Lines[referenceIndexes[^1]], include)));
    }

    private static Int32 GetInsertionIndex(TextLines lines, IReadOnlyList<Int32> referenceIndexes, String include) =>
        referenceIndexes
            .Where(index => CodeLayout.PathOrder.Compare(ReadInclude(lines.Lines[index]), include) > 0)
            .DefaultIfEmpty(referenceIndexes[^1] + 1)
            .First();

    private static String BuildReference(String neighbour, String include) =>
        $"{TextLines.ReadIndentation(neighbour)}<ProjectReference Include=\"{include}\" />";

    private static Boolean Includes(String line, String include) =>
        String.Equals(ReadInclude(line), include, StringComparison.OrdinalIgnoreCase);

    private static String ReadInclude(String line) => ProjectReferencePattern.Match(line).Groups["include"].Value;
}
