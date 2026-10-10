using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.Scaffold.Sources;

internal static class SyntaxExtensions
{
    extension(SyntaxNode node)
    {
        public String OneLineText =>
            node
                .NormalizeWhitespace()
                .ToString();

        public Int32 StartLine =>
            node
                .GetLocation()
                .GetLineSpan()
                .StartLinePosition.Line + 1;
    }

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

        public IEnumerable<String> ContainingTypeNames =>
            declaration
                .Ancestors()
                .OfType<BaseTypeDeclarationSyntax>()
                .Reverse()
                .Select(containingType => containingType.Name);

        public String FullName =>
            String.Join('.', [declaration.Namespace, .. declaration.ContainingTypeNames, declaration.Name]);

        public Boolean IsTopLevel => declaration.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

        public Boolean IsAbstract => declaration.Modifiers.Any(SyntaxKind.AbstractKeyword);

        public IEnumerable<TypeSyntax> BaseTypes =>
            declaration.BaseList is { } baseList ? baseList.Types.Select(baseType => baseType.Type) : [];
    }

    extension(TypeDeclarationSyntax declaration)
    {
        public IReadOnlyList<ParameterSyntax> PositionalParameters =>
            declaration.ParameterList is { } parameterList ? parameterList.Parameters : [];
    }

    extension(FieldDeclarationSyntax fieldDeclaration)
    {
        public Boolean IsConstant => fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword);
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
