using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class RouteVerifier(IReadOnlyList<SourceProject> moduleProjects)
{
    public Result<Unit> Verify(ScaffoldedFeature feature)
    {
        var mappings = moduleProjects
            .SelectMany(project => project.GetNodes<InvocationExpressionSyntax>(feature.MapMethod))
            .Where(invocation => Maps(invocation, feature.MapMethod, feature.Route))
            .Select(invocation => $"{invocation.SyntaxTree.FilePath}:{invocation.ArgumentList.StartLine}")
            .Order(StringComparer.Ordinal)
            .ToList();

        return mappings.Count == 0
            ? Unit.Value
            : Error.Failure(
                "scaffold.route",
                $"feature {feature.Name} of {feature.Module.Name} would answer {feature.HttpMethod} "
                + $"{feature.HttpPath}, which {String.Join(", ", mappings)} maps already; "
                + $"choose another {feature.RouteChoice}");
    }

    private static Boolean Maps(InvocationExpressionSyntax invocation, String mapMethod, EndpointRoute route) =>
        invocation is
            {
                Expression: MemberAccessExpressionSyntax mapAccess,
                ArgumentList.Arguments: [var routeArgument, ..],
            }
        && mapAccess.Name.Identifier.Text == mapMethod
        && IsCalledOnAParameter(mapAccess)
        && FindMappedPath(routeArgument.Expression)
            .Where(route.Matches)
            .HasValue;

    private static Boolean IsCalledOnAParameter(MemberAccessExpressionSyntax mapAccess) =>
        mapAccess.Expression is IdentifierNameSyntax receiver
        && mapAccess
            .Ancestors()
            .OfType<BaseMethodDeclarationSyntax>()
            .Take(1)
            .Any(method => method.ParameterList.Parameters.Any(parameter =>
                parameter.Identifier.Text == receiver.Identifier.Text));

    private static Optional<String> FindMappedPath(ExpressionSyntax expression) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.StringLiteralExpression =>
                literal.Token.ValueText,
            MemberAccessExpressionSyntax memberAccess when IsStringEmpty(memberAccess) => String.Empty,
            _ => Optional<String>.None(),
        };

    private static Boolean IsStringEmpty(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess is
        {
            Expression: IdentifierNameSyntax { Identifier.Text: nameof(String) },
            Name.Identifier.Text: nameof(String.Empty),
        };
}
