using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class SourceSyntaxExtensions
{
    private const String AttributeSuffix = "Attribute";

    extension(Location location)
    {
        public Int32 StartLine => location.GetLineSpan().StartLinePosition.Line + 1;
    }

    extension(TypeSyntax typeSyntax)
    {
        public String SimpleName =>
            typeSyntax switch
            {
                QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
                SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
                _ => "",
            };

        public Boolean NamesAttribute(String attributeName) =>
            typeSyntax.SimpleName == attributeName || typeSyntax.SimpleName == attributeName + AttributeSuffix;
    }
}
