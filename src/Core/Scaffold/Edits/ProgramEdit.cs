using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class ProgramEdit
{
    private const String AddServicesDeclaration = "static void AddServices(WebApplicationBuilder builder)";

    private const String BodyOpening = "{";

    private const String BodyIndentation = "    ";

    private const String PlatformCall = "builder.AddPlatform();";

    private const String ModuleApiNamespace = "Tsikhanau.Monolith.Api.Modules";

    private const String CoreNamespacePrefix = "Tsikhanau.";

    [GeneratedRegex(@"^using (?<namespace>[A-Za-z_][\w.]*);$")]
    private static partial Regex UsingPattern { get; }

    [GeneratedRegex(@"^\s*builder\.AddModule<(?:\w+\.)*(?<module>\w+?)(?:Module)?>\(\);\s*$")]
    private static partial Regex ModuleCallPattern { get; }

    public static Result<FileEdit> AddModule(TextFile program, ScaffoldedModule module) =>
        program.PlanEdit(text => AddModuleLines(TextLines.Split(text, program.NewLine), program.Path, module));

    private static Result<String> AddModuleLines(TextLines lines, String path, ScaffoldedModule module)
    {
        var moduleCall = $"builder.AddModule<{module.ModuleClass}>();";
        var moduleUsing = $"using {module.Namespace};";
        var usingIndexes = lines.GetIndexes(UsingPattern.IsMatch);

        return FindModuleCallAnchor(lines, path)
            .Ensure(_ => usingIndexes.Count > 0, BuildAnchorError(path, "using directives at the top"))
            .Ensure(_ => !lines.Contains(moduleCall), BuildAnchorError(path, $"no {moduleCall} yet"))
            .Ensure(_ => !lines.Contains(moduleUsing), BuildAnchorError(path, $"no {moduleUsing} yet"))
            .Map(anchor => AddModuleCall(lines, anchor, module.Name, moduleCall))
            .Map(withCall => AddUsing(withCall, usingIndexes, module.Namespace))
            .Map(AddModuleApiUsingWhenMissing)
            .Map(updatedLines => updatedLines.Join());
    }

    private static Result<ModuleCallAnchor> FindModuleCallAnchor(TextLines lines, String path)
    {
        var platformIndexes = lines.GetIndexes(line => line.Trim() == PlatformCall);

        return platformIndexes.Count > 0
            ? FindPlatformCallAnchor(lines, path, platformIndexes)
            : FindAddServicesAnchor(lines, path);
    }

    private static Result<ModuleCallAnchor> FindPlatformCallAnchor(
        TextLines lines,
        String path,
        IReadOnlyList<Int32> platformIndexes) =>
        platformIndexes is [var platformIndex]
            ? new ModuleCallAnchor(platformIndex, TextLines.ReadIndentation(lines.Lines[platformIndex]))
            : BuildAnchorError(path, $"exactly one line {PlatformCall}");

    private static Result<ModuleCallAnchor> FindAddServicesAnchor(TextLines lines, String path)
    {
        var declarationIndexes = lines.GetIndexes(line => line.Trim() == AddServicesDeclaration);

        return declarationIndexes is [var declarationIndex] && OpensBody(lines, declarationIndex + 1)
            ? new ModuleCallAnchor(declarationIndex + 1, ReadBodyIndentation(lines.Lines[declarationIndex + 1]))
            : BuildAnchorError(path, $"exactly one line {AddServicesDeclaration} followed by a line {BodyOpening}");
    }

    private static Boolean OpensBody(TextLines lines, Int32 index) =>
        index < lines.Lines.Count && lines.Lines[index].Trim() == BodyOpening;

    private static String ReadBodyIndentation(String openingLine) =>
        TextLines.ReadIndentation(openingLine) + BodyIndentation;

    private static TextLines AddModuleApiUsingWhenMissing(TextLines lines) =>
        lines.Contains($"using {ModuleApiNamespace};")
            ? lines
            : AddUsing(lines, GetCoreUsingIndexes(lines), ModuleApiNamespace);

    private static IReadOnlyList<Int32> GetCoreUsingIndexes(TextLines lines)
    {
        var coreUsingIndexes = lines.GetIndexes(IsCoreUsing);
        return coreUsingIndexes.Count > 0 ? coreUsingIndexes : lines.GetIndexes(UsingPattern.IsMatch);
    }

    private static TextLines AddModuleCall(
        TextLines lines,
        ModuleCallAnchor anchor,
        String moduleName,
        String moduleCall)
    {
        var moduleCalls = lines.Lines
            .Skip(anchor.Index + 1)
            .TakeWhile(IsModuleCall)
            .Select((line, offset) => (ModuleName: ReadModuleName(line), Index: anchor.Index + 1 + offset))
            .ToList();

        var insertionIndex = moduleCalls
            .Where(existing => CodeLayout.NameOrder.Compare(existing.ModuleName, moduleName) > 0)
            .Select(existing => existing.Index)
            .DefaultIfEmpty(anchor.Index + 1 + moduleCalls.Count)
            .First();

        return lines.Insert(insertionIndex, $"{anchor.Indentation}{moduleCall}");
    }

    private static TextLines AddUsing(TextLines lines, IReadOnlyList<Int32> usingIndexes, String moduleNamespace)
    {
        var insertionIndex = usingIndexes
            .Where(index => CodeLayout.NamespaceOrder.Compare(ReadNamespace(lines.Lines[index]), moduleNamespace) > 0)
            .DefaultIfEmpty(usingIndexes[^1] + 1)
            .First();

        return lines.Insert(insertionIndex, $"using {moduleNamespace};");
    }

    private static Boolean IsModuleCall(String line) => ModuleCallPattern.IsMatch(line);

    private static Boolean IsCoreUsing(String line) =>
        UsingPattern.IsMatch(line) && IsCoreNamespace(ReadNamespace(line));

    private static Boolean IsCoreNamespace(String usedNamespace) =>
        usedNamespace.StartsWith(CoreNamespacePrefix, StringComparison.Ordinal);

    private static String ReadModuleName(String line) => ModuleCallPattern.Match(line).Groups["module"].Value;

    private static String ReadNamespace(String line) => UsingPattern.Match(line).Groups["namespace"].Value;

    private static Error BuildAnchorError(String path, String expected) =>
        Error.Failure("scaffold.anchor", $"{path} must hold {expected}");

    private sealed record ModuleCallAnchor(Int32 Index, String Indentation);
}
