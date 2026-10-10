using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed record SourceProject(String ModuleName, Boolean IsContracts, IReadOnlyList<SourceFile> Files)
{
    public String Role => IsContracts ? $"the Contracts of {ModuleName}" : $"the module project of {ModuleName}";

    public IEnumerable<T> FindDeclarations<T>(String name)
        where T : BaseTypeDeclarationSyntax =>
        FindNodes<T>(name);

    public IEnumerable<T> FindNodes<T>(String mentionedName)
        where T : SyntaxNode =>
        Files
            .Where(file => file.Mentions(mentionedName))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<T>());
}
