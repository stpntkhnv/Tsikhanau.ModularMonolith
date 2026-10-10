using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class ModuleProjectEdit
{
    private const String SelfClosingEnd = "/>";

    private const String ClosingTag = "</ProjectReference>";

    private const String OpeningTag = "<ProjectReference";

    private const String OneLineTags =
        "scaffold edits references whose tag is one line, <ProjectReference Include=\"...\" /> or "
        + "<ProjectReference Include=\"...\"> with its children below, so write the tag on one line and run "
        + "scaffold again";

    [GeneratedRegex("^\\s*<ProjectReference\\s+Include=\"(?<include>[^\"]*)\"")]
    private static partial Regex ProjectReferencePattern { get; }

    public static Result<FileEdit> AddNeighbourContracts(
        TextFile moduleProject,
        ScaffoldedModule module,
        String neighbour) =>
        moduleProject.PlanEdit(text =>
            EnsureOneLineTags(TextLines.Split(text, moduleProject.NewLine), moduleProject.Path)
                .Bind(lines => AddReference(lines, moduleProject.Path, module, neighbour)));

    private static Result<TextLines> EnsureOneLineTags(TextLines lines, String path)
    {
        var splitTags = lines
            .GetIndexes(line => line.Contains(OpeningTag, StringComparison.Ordinal))
            .Where(start => !HasOneLineTag(lines, start))
            .Select(start => $"{path}:{start + 1} holds a <ProjectReference> tag that does not end on that line")
            .ToList();

        return splitTags.Count == 0
            ? lines
            : Error.Failure("scaffold.anchor", $"{String.Join("; ", splitTags)}; {OneLineTags}");
    }

    private static Boolean HasOneLineTag(TextLines lines, Int32 start) =>
        IsSelfClosing(lines.Lines[start]) || EndsTag(lines.Lines[start]) && FindClosingTag(lines, start).HasValue;

    private static Boolean EndsTag(String line) =>
        line
            .TrimEnd()
            .EndsWith('>');

    private static Result<String> AddReference(
        TextLines lines,
        String path,
        ScaffoldedModule module,
        String neighbour)
    {
        var ownContractsInclude = $@"..\{module.ContractsProjectName}\{module.ContractsProjectName}.csproj";
        var neighbourContracts = $"{module.App.ProjectPrefix}.Modules.{neighbour}.Contracts";
        var include = $@"..\..\{neighbour}\{neighbourContracts}\{neighbourContracts}.csproj";
        var contractsReferences = ReadContractsReferences(lines, module.App.ProjectPrefix);
        var neighbourReferences = contractsReferences
            .Where(contractsReference => contractsReference.Module != module.Name)
            .ToList();

        Optional<ContractsReference> ownContracts = contractsReferences.FirstOrDefault(
            contractsReference => contractsReference.Module == module.Name);

        return ownContracts.Match(
            ownReference => Result.Success(lines
                .Insert(
                    GetInsertionIndex(neighbourReferences, neighbour, ownReference.End + 1),
                    BuildReference(lines.Lines[ownReference.Start], include))
                .Join()),
            () => Error.Failure("scaffold.anchor", $"{path} must hold the reference to {ownContractsInclude}"));
    }

    private static IReadOnlyList<ContractsReference> ReadContractsReferences(TextLines lines, String projectPrefix) =>
        [
            .. lines
                .GetIndexes(ProjectReferencePattern.IsMatch)
                .Select(start => (Start: start, Module: FindModuleOfContracts(lines.Lines[start], projectPrefix)))
                .Where(reference => reference.Module.HasValue)
                .Select(reference => new ContractsReference(
                    reference.Module.Value,
                    reference.Start,
                    GetEnd(lines, reference.Start))),
        ];

    private static Optional<String> FindModuleOfContracts(String line, String projectPrefix) =>
        ModuleGraph.FindModuleOfContracts(ProjectReferencePattern.Match(line).Groups["include"].Value, projectPrefix);

    private static Int32 GetEnd(TextLines lines, Int32 start) =>
        IsSelfClosing(lines.Lines[start])
            ? start
            : FindClosingTag(lines, start)
                .GetValueOrDefault(start);

    private static Optional<Int32> FindClosingTag(TextLines lines, Int32 start) =>
        lines
            .GetIndexes(line => line.Contains(ClosingTag, StringComparison.Ordinal))
            .Where(index => index >= start)
            .Select(Optional<Int32>.Some)
            .FirstOrDefault(Optional<Int32>.None());

    private static Boolean IsSelfClosing(String line) =>
        line
            .TrimEnd()
            .EndsWith(SelfClosingEnd, StringComparison.Ordinal);

    private static Int32 GetInsertionIndex(
        IReadOnlyList<ContractsReference> neighbourReferences,
        String neighbour,
        Int32 afterOwnContracts) =>
        neighbourReferences
            .Where(neighbourReference => CodeLayout.NameOrder.Compare(neighbourReference.Module, neighbour) > 0)
            .Select(neighbourReference => neighbourReference.Start)
            .DefaultIfEmpty(neighbourReferences is [.., var lastReference] ? lastReference.End + 1 : afterOwnContracts)
            .First();

    private static String BuildReference(String ownContractsLine, String include) =>
        $"{TextLines.ReadIndentation(ownContractsLine)}<ProjectReference Include=\"{include}\" />";

    private sealed record ContractsReference(String Module, Int32 Start, Int32 End);
}
