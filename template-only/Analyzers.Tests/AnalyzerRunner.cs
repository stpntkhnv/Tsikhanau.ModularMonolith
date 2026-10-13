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

    private static readonly CSharpCompilationOptions LibraryOptions = new(OutputKind.DynamicallyLinkedLibrary);

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

    public static Task<IReadOnlyList<Int32>> GetReportedLinesOfValidSourceAsync(
        DiagnosticAnalyzer analyzer,
        String source) =>
        GetReportedLinesOfValidSourcesAsync(analyzer, [source]);

    public static async Task<IReadOnlyList<Int32>> GetReportedLinesOfValidSourcesAsync(
        DiagnosticAnalyzer analyzer,
        IReadOnlyList<String> sources)
    {
        var diagnostics = await GetDiagnosticsOfValidSourcesAsync(analyzer, sources);

        return diagnostics
            .Select(GetLineNumber)
            .Order()
            .ToList();
    }

    public static async Task<IReadOnlyList<String>> GetReportedMessagesOfValidSourceAsync(
        DiagnosticAnalyzer analyzer,
        String source)
    {
        var diagnostics = await GetDiagnosticsOfValidSourcesAsync(analyzer, [source]);

        return diagnostics
            .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .ToList();
    }

    private static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DiagnosticAnalyzer analyzer, String source) =>
        CSharpCompilation
            .Create("Sample", [CSharpSyntaxTree.ParseText(source)], RuntimeReferences)
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    private static Task<ImmutableArray<Diagnostic>> GetDiagnosticsOfValidSourcesAsync(
        DiagnosticAnalyzer analyzer,
        IReadOnlyList<String> sources)
    {
        var compilation = CSharpCompilation.Create(
            "Sample",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            RuntimeReferences,
            LibraryOptions);

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(IsError)
            .Select(DescribeError)
            .ShouldBeEmpty("the sample must compile");

        return compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private static Boolean IsError(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error;

    private static String DescribeError(Diagnostic diagnostic) =>
        $"line {GetLineNumber(diagnostic)}: {diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";

    private static Int32 GetLineNumber(Diagnostic diagnostic) =>
        diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
}
