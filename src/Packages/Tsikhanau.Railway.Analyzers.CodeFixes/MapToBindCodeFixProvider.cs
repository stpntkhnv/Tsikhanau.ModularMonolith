using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Railway.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MapToBindCodeFixProvider))]
[Shared]
public sealed class MapToBindCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<String> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.MapReturnsResult);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not SimpleNameSyntax name)
            {
                continue;
            }

            var map = name.Identifier.ValueText;
            var bind = map == "MapAsync" ? "BindAsync" : "Bind";
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Replace '{map}' with '{bind}'",
                    cancellationToken => ReplaceAsync(context.Document, name, bind, cancellationToken),
                    equivalenceKey: nameof(MapToBindCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Document> ReplaceAsync(Document document, SimpleNameSyntax name, String bind, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var identifier = SyntaxFactory.Identifier(bind).WithTriviaFrom(name.Identifier);
        var replacement = name is GenericNameSyntax generic
            ? ReplaceGeneric(generic, identifier)
            : name.WithIdentifier(identifier);

        return document.WithSyntaxRoot(root.ReplaceNode(name, replacement));
    }

    private static SimpleNameSyntax ReplaceGeneric(GenericNameSyntax generic, SyntaxToken identifier)
    {
        var arguments = generic.TypeArgumentList.Arguments;
        if (arguments.Count != 2 || GetSingleTypeArgument(arguments[1]) is not { } inner)
        {
            return SyntaxFactory.IdentifierName(identifier);
        }

        return generic
            .WithIdentifier(identifier)
            .WithTypeArgumentList(generic.TypeArgumentList.WithArguments(
                arguments.Replace(arguments[1], inner.WithTriviaFrom(arguments[1]))));
    }

    private static TypeSyntax? GetSingleTypeArgument(TypeSyntax type)
    {
        var name = type switch
        {
            GenericNameSyntax generic => generic,
            QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic,
            _ => null
        };

        return name?.TypeArgumentList.Arguments is { Count: 1 } arguments ? arguments[0] : null;
    }
}
