using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.InternalCommands;

internal sealed class InternalCommandNameVerifier(String moduleName, IReadOnlyList<SourceProject> moduleProjects)
{
    private const String AttributeName = "InternalCommandName";

    private const String NameIsStored =
        "the name is stored in the queue and must be unique in the module, so choose another feature name";

    private const String ReadableForms =
        "it reads string literals, their concatenations and interpolations, nameof and the string constants "
        + "declared in the module, so write the name in one of these forms";

    private static readonly String[] AttributeNames = [AttributeName, $"{AttributeName}Attribute"];

    private readonly StringConstantEvaluator _stringConstantEvaluator = new(moduleProjects);

    public Result<Unit> EnsureFree(String name)
    {
        var nameDeclarations = ReadNameDeclarations();

        var holderPaths = nameDeclarations
            .Where(nameDeclaration => nameDeclaration.Name == name)
            .Select(nameDeclaration => nameDeclaration.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unreadableNames = nameDeclarations
            .Where(nameDeclaration => nameDeclaration.Name.IsNone)
            .Select(nameDeclaration => nameDeclaration.Describe())
            .ToList();

        return (holderPaths, unreadableNames) switch
        {
            ([], []) => Unit.Value,
            ([], _) => BuildError(
                $"cannot tell whether {name} is free in {moduleName}: scaffold cannot read the internal command name "
                + $"of {String.Join(", ", unreadableNames)}; {ReadableForms}"),
            _ => BuildError(
                $"{moduleName} already has an internal command named {name} ({String.Join(", ", holderPaths)}); "
                + NameIsStored),
        };
    }

    private IReadOnlyList<NameDeclaration> ReadNameDeclarations() =>
        [
            .. moduleProjects
                .SelectMany(project => project.FindNodes<AttributeSyntax>(AttributeName))
                .Where(attribute => AttributeNames.Contains(attribute.Name.SimpleName))
                .Select(attribute => new NameDeclaration(attribute, FindName(attribute)))
                .OrderBy(nameDeclaration => nameDeclaration.Path, StringComparer.Ordinal),
        ];

    private Optional<String> FindName(AttributeSyntax attribute) =>
        attribute.ArgumentList is { Arguments: [var nameArgument, ..] }
            ? _stringConstantEvaluator.FindValue(nameArgument.Expression)
            : Optional<String>.None();

    private static Error BuildError(String message) => Error.Failure("scaffold.internal-command", message);

    private sealed record NameDeclaration(AttributeSyntax Attribute, Optional<String> Name)
    {
        public String Path => Attribute.SyntaxTree.FilePath;

        public Int32 Line => Attribute.StartLine;

        public String Describe() => $"[{Attribute.OneLineText}] ({Path}:{Line})";
    }
}
