namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class GeneratedFileNames
{
    public const String MarksGeneratedCode = "marks the file as generated";

    private const String TemporaryGeneratedFilePrefix = "TemporaryGeneratedFile_";

    private const String DotnetEfDesignerEnding = ".Designer.cs";

    private static readonly String[] GeneratedEndings = [".g.cs", ".g.i.cs", ".generated.cs", ".designer.cs"];

    public static IEnumerable<SourceFinding> Collect(String fileName) =>
        IsGenerated(fileName) ? [new SourceFinding(1, $"the name {fileName} {MarksGeneratedCode}")] : [];

    public static IEnumerable<SourceFinding> CollectExceptDotnetEfDesigner(String fileName) =>
        fileName.EndsWith(DotnetEfDesignerEnding, StringComparison.Ordinal) ? [] : Collect(fileName);

    private static Boolean IsGenerated(String fileName) =>
        fileName.StartsWith(TemporaryGeneratedFilePrefix, StringComparison.OrdinalIgnoreCase)
        || GeneratedEndings.Any(ending => fileName.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
}
