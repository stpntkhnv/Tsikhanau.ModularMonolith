using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal static class FixtureCompiler
{
    private static readonly CSharpCompilationOptions CompilationOptions =
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release);

    public static IReadOnlyList<MetadataReference> RuntimeReferences { get; } =
    [
        .. ((String)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    public static Byte[] Compile(String assemblyName, String source, IEnumerable<MetadataReference> references) =>
        Compile(assemblyName, [CSharpSyntaxTree.ParseText(source)], references);

    public static Byte[] Compile(
        String assemblyName,
        IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(assemblyName, syntaxTrees, references, CompilationOptions);

        using var imageStream = new MemoryStream();
        var emitResult = compilation.Emit(imageStream);

        GetErrors(emitResult)
            .ShouldBeEmpty($"{assemblyName} does not compile");

        return imageStream.ToArray();
    }

    private static IEnumerable<String> GetErrors(EmitResult emitResult) =>
        emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString());
}
