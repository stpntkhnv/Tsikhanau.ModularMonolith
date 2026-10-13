using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EndpointBranchingAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0007",
            "An endpoint holds no branching",
            "An endpoint holds no branching: it maps the request to a command or query and the result to HTTP; move "
            + "the decision into the handler or the domain",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private const String EndpointInterfaceName = "Tsikhanau.Monolith.Api.Endpoints.IEndpoint";

    private static readonly ImmutableArray<SyntaxKind> BranchingKinds = ImmutableArray.Create(
        SyntaxKind.IfStatement,
        SyntaxKind.SwitchStatement,
        SyntaxKind.SwitchExpression,
        SyntaxKind.ConditionalExpression,
        SyntaxKind.CoalesceExpression,
        SyntaxKind.CoalesceAssignmentExpression,
        SyntaxKind.LogicalAndExpression,
        SyntaxKind.LogicalOrExpression,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.TryStatement,
        SyntaxKind.ThrowStatement,
        SyntaxKind.ThrowExpression,
        SyntaxKind.GotoStatement,
        SyntaxKind.GotoCaseStatement,
        SyntaxKind.GotoDefaultStatement);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(RegisterEndpointAction);
    }

    private static void RegisterEndpointAction(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(EndpointInterfaceName) is { } endpointInterface)
        {
            context.RegisterSymbolStartAction(
                symbolContext => RegisterBranchingAction(symbolContext, endpointInterface),
                SymbolKind.NamedType);
        }
    }

    private static void RegisterBranchingAction(SymbolStartAnalysisContext context, INamedTypeSymbol endpointInterface)
    {
        if (IsInEndpoint((INamedTypeSymbol)context.Symbol, endpointInterface))
        {
            context.RegisterSyntaxNodeAction(ReportBranching, BranchingKinds);
        }
    }

    private static Boolean IsInEndpoint(INamedTypeSymbol type, INamedTypeSymbol endpointInterface) =>
        IsEndpoint(type, endpointInterface) || IsNestedInEndpoint(type, endpointInterface);

    private static Boolean IsNestedInEndpoint(INamedTypeSymbol type, INamedTypeSymbol endpointInterface) =>
        type.ContainingType is { } containingType && IsInEndpoint(containingType, endpointInterface);

    private static Boolean IsEndpoint(INamedTypeSymbol type, INamedTypeSymbol endpointInterface) =>
        type.AllInterfaces.Contains(endpointInterface, SymbolEqualityComparer.Default);

    private static void ReportBranching(SyntaxNodeAnalysisContext context)
    {
        var firstToken = context.Node.GetFirstToken();
        context.ReportDiagnostic(Diagnostic.Create(Rule, firstToken.GetLocation()));
    }
}
