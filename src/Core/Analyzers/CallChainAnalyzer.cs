using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CallChainAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "TS0001",
        "Put each call of a chain on its own line",
        "Call '{0}' is part of a chain and must start its own line",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCall, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCall(SyntaxNodeAnalysisContext context)
    {
        var call = (InvocationExpressionSyntax)context.Node;
        if (call.Expression is MemberAccessExpressionSyntax memberAccess && NeedsOwnLine(call, memberAccess))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                memberAccess.OperatorToken.GetLocation(),
                memberAccess.Name.Identifier.Text);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static Boolean NeedsOwnLine(InvocationExpressionSyntax call, MemberAccessExpressionSyntax memberAccess) =>
        IsChained(call) && !StartsLine(memberAccess.OperatorToken);

    private static Boolean IsChained(InvocationExpressionSyntax call) =>
        HasPreviousCall(call) || HasNextCall(call);

    private static Boolean HasPreviousCall(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax memberAccess
        && GetReceiver(memberAccess.Expression) is InvocationExpressionSyntax;

    private static ExpressionSyntax GetReceiver(ExpressionSyntax expression) =>
        expression switch
        {
            MemberAccessExpressionSyntax memberAccess => GetReceiver(memberAccess.Expression),
            PostfixUnaryExpressionSyntax nullSuppression when IsNullSuppression(nullSuppression) =>
                GetReceiver(nullSuppression.Operand),
            ParenthesizedExpressionSyntax { Expression: not AwaitExpressionSyntax } parenthesizedExpression =>
                GetReceiver(parenthesizedExpression.Expression),
            _ => expression,
        };

    private static Boolean HasNextCall(ExpressionSyntax expression) =>
        expression.Parent switch
        {
            ParenthesizedExpressionSyntax parenthesizedExpression => HasNextCall(parenthesizedExpression),
            PostfixUnaryExpressionSyntax nullSuppression when IsNullSuppression(nullSuppression) =>
                HasNextCall(nullSuppression),
            MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == expression =>
                IsCall(memberAccess) || HasNextCall(memberAccess),
            _ => false,
        };

    private static Boolean IsCall(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Parent is InvocationExpressionSyntax call && call.Expression == memberAccess;

    private static Boolean IsNullSuppression(PostfixUnaryExpressionSyntax expression) =>
        expression.IsKind(SyntaxKind.SuppressNullableWarningExpression);

    private static Boolean StartsLine(SyntaxToken token) =>
        token.GetPreviousToken().EndLine < token.StartLine;
}
