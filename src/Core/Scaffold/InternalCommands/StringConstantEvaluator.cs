using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.InternalCommands;

internal sealed class StringConstantEvaluator(IReadOnlyList<SourceProject> projects)
{
    private const Int32 MaxConstantDepth = 32;

    public Optional<String> FindValue(ExpressionSyntax expression) => FindValue(expression, constantDepth: 0);

    private Optional<String> FindValue(ExpressionSyntax expression, Int32 constantDepth) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.StringLiteralExpression =>
                literal.Token.ValueText,
            ParenthesizedExpressionSyntax parenthesized => FindValue(parenthesized.Expression, constantDepth),
            BinaryExpressionSyntax concatenation when concatenation.Kind() == SyntaxKind.AddExpression =>
                Concatenate(
                [
                    FindValue(concatenation.Left, constantDepth),
                    FindValue(concatenation.Right, constantDepth),
                ]),
            InterpolatedStringExpressionSyntax interpolation =>
                Concatenate(interpolation.Contents.Select(content => FindValue(content, constantDepth))),
            InvocationExpressionSyntax invocation when IsNameOf(invocation) => FindNameOf(invocation),
            IdentifierNameSyntax or MemberAccessExpressionSyntax when constantDepth < MaxConstantDepth =>
                FindConstant(expression)
                    .Bind(constant => FindValue(constant, constantDepth + 1)),
            _ => Optional<String>.None(),
        };

    private Optional<String> FindValue(InterpolatedStringContentSyntax content, Int32 constantDepth) =>
        content switch
        {
            InterpolatedStringTextSyntax text => text.TextToken.ValueText,
            InterpolationSyntax { AlignmentClause: null, FormatClause: null } hole =>
                FindValue(hole.Expression, constantDepth),
            _ => Optional<String>.None(),
        };

    private static Optional<String> Concatenate(IEnumerable<Optional<String>> parts) =>
        parts.Aggregate(
            Optional<String>.Some(String.Empty),
            (text, part) => text.Bind(start => part.Map(end => start + end)));

    private static Boolean IsNameOf(InvocationExpressionSyntax invocation) =>
        invocation is
        {
            Expression: IdentifierNameSyntax { Identifier.Text: "nameof" },
            ArgumentList.Arguments.Count: 1,
        };

    private static Optional<String> FindNameOf(InvocationExpressionSyntax invocation) =>
        FindLastName(invocation.ArgumentList.Arguments[0].Expression);

    private Optional<ExpressionSyntax> FindConstant(ExpressionSyntax reference) =>
        reference switch
        {
            IdentifierNameSyntax name => FindConstant(reference, Optional<String>.None(), name.Identifier.Text),
            MemberAccessExpressionSyntax memberAccess => FindLastName(memberAccess.Expression)
                .Bind(typeName => FindConstant(reference, typeName, memberAccess.Name.Identifier.Text)),
            _ => Optional<ExpressionSyntax>.None(),
        };

    private Optional<ExpressionSyntax> FindConstant(
        ExpressionSyntax reference,
        Optional<String> typeName,
        String constantName)
    {
        var constants = FindConstants(constantName)
            .Where(constant => typeName.Match(constant.IsDeclaredIn, () => true))
            .ToList();

        var enclosingConstants = constants
            .Where(constant => constant.Encloses(reference))
            .ToList();

        return (enclosingConstants, constants) switch
        {
            ([var enclosingConstant], _) => enclosingConstant.Value,
            (_, [var constant]) => constant.Value,
            _ => Optional<ExpressionSyntax>.None(),
        };
    }

    private IEnumerable<StringConstant> FindConstants(String constantName) =>
        projects
            .SelectMany(project => project.FindNodes<FieldDeclarationSyntax>(constantName))
            .Where(fieldDeclaration => fieldDeclaration.IsConstant)
            .SelectMany(fieldDeclaration => fieldDeclaration.Declaration.Variables
                .Where(variable => variable.Identifier.Text == constantName)
                .SelectMany(variable => BuildConstant(fieldDeclaration, variable)));

    private static IEnumerable<StringConstant> BuildConstant(
        FieldDeclarationSyntax fieldDeclaration,
        VariableDeclaratorSyntax variable) =>
        (fieldDeclaration.Parent, variable.Initializer) is (TypeDeclarationSyntax type, { } initializer)
            ? [new StringConstant(type, initializer.Value)]
            : [];

    private static Optional<String> FindLastName(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleNameSyntax name => name.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.Text,
            _ => Optional<String>.None(),
        };

    private sealed record StringConstant(TypeDeclarationSyntax Type, ExpressionSyntax Value)
    {
        public Boolean IsDeclaredIn(String typeName) => Type.Identifier.Text == typeName;

        public Boolean Encloses(ExpressionSyntax reference) =>
            reference
                .Ancestors()
                .Contains(Type);
    }
}
