using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers.Tests;

internal static class AnalyzerRunner
{
    private static readonly IReadOnlyList<MetadataReference> RuntimeReferences =
        ((String)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();

    public static async Task<IReadOnlyList<Int32>> GetReportedLinesAsync(DiagnosticAnalyzer analyzer, String source)
    {
        var compilation = CSharpCompilation.Create("Sample", [CSharpSyntaxTree.ParseText(source)], RuntimeReferences);
        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        return diagnostics
            .Select(GetLineNumber)
            .Order()
            .ToList();
    }

    private static Int32 GetLineNumber(Diagnostic diagnostic) =>
        diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
}
