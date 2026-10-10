using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Sources;

internal sealed record SourceProject(String ModuleName, Boolean IsContracts, IReadOnlyList<SourceFile> Files)
{
    public String Role => IsContracts ? $"the Contracts of {ModuleName}" : $"the module project of {ModuleName}";

    public IEnumerable<T> GetDeclarations<T>(String name)
        where T : BaseTypeDeclarationSyntax =>
        GetNodes<T>(name)
            .Where(declaration => declaration.IsTopLevel);

    public Boolean DeclaresType(String typeName) =>
        GetDeclarations<BaseTypeDeclarationSyntax>(typeName)
            .Any(declaration => declaration.Name == typeName);

    public Optional<T> FindDeclaration<T>(String name)
        where T : BaseTypeDeclarationSyntax =>
        GetDeclarations<T>(name)
            .Where(declaration => declaration.Name == name)
            .ToList() is [var declaration]
            ? declaration
            : Optional<T>.None();

    public IEnumerable<T> GetNodes<T>(String mentionedName)
        where T : CSharpSyntaxNode =>
        Files
            .Where(file => file.Mentions(mentionedName))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<T>());
}
