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
                + $"{feature.HttpPath}, which {String.Join(", ", mappings)} maps already; choose another feature name");
    }

    private static Boolean Maps(InvocationExpressionSyntax invocation, String mapMethod, String route) =>
        invocation is
            {
                Expression: MemberAccessExpressionSyntax mapAccess,
                ArgumentList.Arguments: [var routeArgument, ..],
            }
        && mapAccess.Name.Identifier.Text == mapMethod
        && IsCalledOnAParameter(mapAccess)
        && IsStringLiteral(routeArgument.Expression, route);

    private static Boolean IsCalledOnAParameter(MemberAccessExpressionSyntax mapAccess) =>
        mapAccess.Expression is IdentifierNameSyntax receiver
        && mapAccess
            .Ancestors()
            .OfType<BaseMethodDeclarationSyntax>()
            .Take(1)
            .Any(method => method.ParameterList.Parameters.Any(parameter =>
                parameter.Identifier.Text == receiver.Identifier.Text));

    private static Boolean IsStringLiteral(ExpressionSyntax expression, String text) =>
        expression is LiteralExpressionSyntax literal
        && literal.Kind() == SyntaxKind.StringLiteralExpression
        && literal.Token.ValueText == text;
}
