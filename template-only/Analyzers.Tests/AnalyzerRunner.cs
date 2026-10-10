using System.Collections.Immutable;
using System.Globalization;
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
        var diagnostics = await GetDiagnosticsAsync(analyzer, source);

        return diagnostics
            .Select(GetLineNumber)
            .Order()
            .ToList();
    }

    public static async Task<IReadOnlyList<String>> GetReportedMessagesAsync(DiagnosticAnalyzer analyzer, String source)
    {
        var diagnostics = await GetDiagnosticsAsync(analyzer, source);

        return diagnostics
            .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .ToList();
    }

    private static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DiagnosticAnalyzer analyzer, String source) =>
        CSharpCompilation
            .Create("Sample", [CSharpSyntaxTree.ParseText(source)], RuntimeReferences)
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    private static Int32 GetLineNumber(Diagnostic diagnostic) =>
        diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
}
