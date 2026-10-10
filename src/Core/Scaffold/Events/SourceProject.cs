using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed record SourceProject(String ModuleName, Boolean IsContracts, IReadOnlyList<SourceFile> Files)
{
    public String Role => IsContracts ? $"the Contracts of {ModuleName}" : $"the module project of {ModuleName}";

    public IEnumerable<T> FindDeclarations<T>(String name)
        where T : BaseTypeDeclarationSyntax =>
        Files
            .Where(file => file.Mentions(name))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<T>());
}
