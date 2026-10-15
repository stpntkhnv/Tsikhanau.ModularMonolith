using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static partial class ProgramEdit
{
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
        var platformIndexes = lines.GetIndexes(line => line.Trim() == PlatformCall);
        var usingIndexes = lines.GetIndexes(UsingPattern.IsMatch);

        return Result
            .Success(lines)
            .Ensure(_ => platformIndexes.Count == 1, BuildAnchorError(path, $"exactly one line {PlatformCall}"))
            .Ensure(_ => usingIndexes.Count > 0, BuildAnchorError(path, "using directives at the top"))
            .Ensure(_ => !lines.Contains(moduleCall), BuildAnchorError(path, $"no {moduleCall} yet"))
            .Ensure(_ => !lines.Contains(moduleUsing), BuildAnchorError(path, $"no {moduleUsing} yet"))
            .Map(_ => AddModuleCall(lines, platformIndexes[0], module.Name, moduleCall))
            .Map(withCall => AddUsing(withCall, usingIndexes, module.Namespace))
            .Map(AddModuleApiUsingWhenMissing)
            .Map(updatedLines => updatedLines.Join());
    }

    private static TextLines AddModuleApiUsingWhenMissing(TextLines lines) =>
        lines.Contains($"using {ModuleApiNamespace};")
            ? lines
            : AddUsing(lines, GetCoreUsingIndexes(lines), ModuleApiNamespace);

    private static IReadOnlyList<Int32> GetCoreUsingIndexes(TextLines lines)
    {
        var coreUsingIndexes = lines.GetIndexes(IsCoreUsing);
        return coreUsingIndexes.Count > 0 ? coreUsingIndexes : lines.GetIndexes(UsingPattern.IsMatch);
    }

    private static TextLines AddModuleCall(TextLines lines, Int32 platformIndex, String moduleName, String moduleCall)
    {
        var moduleCalls = lines.Lines
            .Skip(platformIndex + 1)
            .TakeWhile(IsModuleCall)
            .Select((line, offset) => (ModuleName: ReadModuleName(line), Index: platformIndex + 1 + offset))
            .ToList();

        var insertionIndex = moduleCalls
            .Where(existing => CodeLayout.NameOrder.Compare(existing.ModuleName, moduleName) > 0)
            .Select(existing => existing.Index)
            .DefaultIfEmpty(platformIndex + 1 + moduleCalls.Count)
            .First();

        var indentation = TextLines.ReadIndentation(lines.Lines[platformIndex]);

        return lines.Insert(insertionIndex, $"{indentation}{moduleCall}");
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
}
