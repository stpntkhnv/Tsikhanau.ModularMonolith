using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.InternalCommands;

internal sealed class InternalCommandNameVerifier(String moduleName, IReadOnlyList<SourceProject> moduleProjects)
{
    private const String AttributeName = "InternalCommandName";

    private const String NameIsStored =
        "the name is stored in the queue and must be unique in the module, so choose another feature name";

    private const String NamesItReads =
        "because it reads an internal command name only as a string literal or a string constant of the module; "
        + "the gate's internal command name rule checks the module's names";

    private static readonly String[] AttributeNames = [AttributeName, $"{AttributeName}Attribute"];

    public Result<Optional<String>> Verify(String name)
    {
        var nameDeclarations = ReadNameDeclarations();

        var holderPaths = nameDeclarations
            .Where(nameDeclaration => nameDeclaration.Name == name)
            .Select(nameDeclaration => nameDeclaration.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var uncomparedNames = nameDeclarations
            .Where(nameDeclaration => nameDeclaration.Name.IsNone)
            .Select(nameDeclaration => nameDeclaration.Describe())
            .ToList();

        return holderPaths.Count == 0
            ? BuildNote(name, uncomparedNames)
            : Error.Failure(
                "scaffold.internal-command",
                $"{moduleName} already has an internal command named {name} ({String.Join(", ", holderPaths)}); "
                + NameIsStored);
    }

    private static Optional<String> BuildNote(String name, IReadOnlyList<String> uncomparedNames) =>
        uncomparedNames.Count == 0
            ? Optional<String>.None()
            : $"it did not compare {name} with {String.Join(", ", uncomparedNames)}, {NamesItReads}";

    private IReadOnlyList<NameDeclaration> ReadNameDeclarations() =>
        [
            .. moduleProjects
                .SelectMany(project => project.GetNodes<AttributeSyntax>(AttributeName))
                .Where(attribute => AttributeNames.Contains(attribute.Name.SimpleName))
                .Select(attribute => new NameDeclaration(attribute, FindName(attribute)))
                .OrderBy(nameDeclaration => nameDeclaration.Path, StringComparer.Ordinal),
        ];

    private Optional<String> FindName(AttributeSyntax attribute) =>
        attribute.ArgumentList is { Arguments: [var nameArgument, ..] }
            ? FindValue(nameArgument.Expression)
            : Optional<String>.None();

    private Optional<String> FindValue(ExpressionSyntax expression) =>
        FindConstantName(expression)
            .Match(
                constantName => FindConstantInitializer(constantName)
                    .Bind(FindLiteral),
                () => FindLiteral(expression));

    private Optional<ExpressionSyntax> FindConstantInitializer(String constantName) =>
        moduleProjects
            .SelectMany(project => project.GetNodes<FieldDeclarationSyntax>(constantName))
            .Where(fieldDeclaration => fieldDeclaration.IsConstant)
            .SelectMany(fieldDeclaration => fieldDeclaration.Declaration.Variables)
            .Where(variable => variable.Identifier.Text == constantName)
            .ToList() is [{ Initializer.Value: var initializer }]
            ? initializer
            : Optional<ExpressionSyntax>.None();

    private static Optional<String> FindConstantName(ExpressionSyntax expression) =>
        expression switch
        {
            IdentifierNameSyntax constant => constant.Identifier.Text,
            MemberAccessExpressionSyntax constant => constant.Name.Identifier.Text,
            _ => Optional<String>.None(),
        };

    private static Optional<String> FindLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression
            ? literal.Token.ValueText
            : Optional<String>.None();

    private sealed record NameDeclaration(AttributeSyntax Attribute, Optional<String> Name)
    {
        public String Path => Attribute.SyntaxTree.FilePath;

        public Int32 Line => Attribute.StartLine;

        public String Describe() => $"[{Attribute.OneLineText}] ({Path}:{Line})";
    }
}
