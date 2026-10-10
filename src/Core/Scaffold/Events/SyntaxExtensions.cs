using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal static class SyntaxExtensions
{
    extension(BaseTypeDeclarationSyntax declaration)
    {
        public String Name => declaration.Identifier.Text;

        public String Namespace =>
            String.Join(
                '.',
                declaration
                    .Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Reverse()
                    .Select(namespaceDeclaration => namespaceDeclaration.Name.ToString()));

        public String FullName => $"{declaration.Namespace}.{declaration.Name}";

        public IEnumerable<TypeSyntax> BaseTypes =>
            declaration.BaseList is { } baseList ? baseList.Types.Select(baseType => baseType.Type) : [];
    }

    extension(TypeDeclarationSyntax declaration)
    {
        public IReadOnlyList<ParameterSyntax> PositionalParameters =>
            declaration.ParameterList is { } parameterList ? parameterList.Parameters : [];
    }

    extension(TypeSyntax type)
    {
        public String SimpleName =>
            type switch
            {
                QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
                AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.Text,
                SimpleNameSyntax simpleName => simpleName.Identifier.Text,
                _ => type.ToString(),
            };

        public IReadOnlyList<TypeSyntax> TypeArguments =>
            type switch
            {
                GenericNameSyntax genericName => genericName.TypeArgumentList.Arguments,
                QualifiedNameSyntax { Right: GenericNameSyntax genericName } => genericName.TypeArgumentList.Arguments,
                _ => [],
            };
    }
}
