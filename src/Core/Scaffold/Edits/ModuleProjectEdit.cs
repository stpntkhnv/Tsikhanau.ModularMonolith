using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class ModuleProjectEdit
{
    [GeneratedRegex("^\\s*<ProjectReference Include=\"(?<include>[^\"]*)\"\\s*/>\\s*$")]
    private static partial Regex ProjectReferencePattern { get; }

    public static Result<FileEdit> AddNeighbourContracts(
        TextFile moduleProject,
        ScaffoldedModule module,
        String neighbour) =>
        moduleProject.PlanEdit(text =>
            AddReference(TextLines.Split(text, moduleProject.NewLine), moduleProject.Path, module, neighbour));

    private static Result<String> AddReference(
        TextLines lines,
        String path,
        ScaffoldedModule module,
        String neighbour)
    {
        var ownContracts = $@"..\{module.ContractsProjectName}\{module.ContractsProjectName}.csproj";
        var neighbourContracts = $"{module.App.ProjectPrefix}.Modules.{neighbour}.Contracts";
        var include = $@"..\..\{neighbour}\{neighbourContracts}\{neighbourContracts}.csproj";
        var neighbourIndexes = lines.FindIndexes(line => IsNeighbourReference(line, module));

        return lines.FindIndexes(line => ReadInclude(line) == ownContracts) is [var ownIndex, ..]
            ? lines
                .Insert(
                    FindInsertionIndex(lines, neighbourIndexes, include, ownIndex + 1),
                    BuildReference(lines.Lines[ownIndex], include))
                .Join()
            : Error.Failure("scaffold.anchor", $"{path} must hold the reference to {ownContracts}");
    }

    private static Int32 FindInsertionIndex(
        TextLines lines,
        IReadOnlyList<Int32> neighbourIndexes,
        String include,
        Int32 afterOwnContracts) =>
        neighbourIndexes
            .Where(index => String.CompareOrdinal(ReadInclude(lines.Lines[index]), include) > 0)
            .DefaultIfEmpty(neighbourIndexes.Count == 0 ? afterOwnContracts : neighbourIndexes[^1] + 1)
            .First();

    private static Boolean IsNeighbourReference(String line, ScaffoldedModule module) =>
        ModuleGraph
            .FindModuleOfContracts(ReadInclude(line), module.App.ProjectPrefix)
            .Where(referencedModule => referencedModule != module.Name)
            .HasValue;

    private static String BuildReference(String ownContractsLine, String include) =>
        $"{TextLines.ReadIndentation(ownContractsLine)}<ProjectReference Include=\"{include}\" />";

    private static String ReadInclude(String line) => ProjectReferencePattern.Match(line).Groups["include"].Value;
}
