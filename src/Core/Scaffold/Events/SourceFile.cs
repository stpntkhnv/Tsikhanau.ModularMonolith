using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed class SourceFile(String path, String text)
{
    private readonly Lazy<CompilationUnitSyntax> _root = new(() => Parse(path, text));

    public CompilationUnitSyntax Root => _root.Value;

    public Boolean Mentions(String name) => text.Contains(name, StringComparison.Ordinal);

    private static CompilationUnitSyntax Parse(String path, String text) =>
        CSharpSyntaxTree
            .ParseText(text, path: path)
            .GetCompilationUnitRoot();
}
