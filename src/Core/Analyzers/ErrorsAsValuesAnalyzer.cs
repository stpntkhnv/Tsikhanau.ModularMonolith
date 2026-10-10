using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorsAsValuesAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0005",
            "Return an Error instead of throwing in Domain and Features",
            "Return an Error instead of throwing in '{0}'",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private static readonly ImmutableHashSet<String> CheckedLayers = ImmutableHashSet.Create("Domain", "Features");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeThrow, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    private static void AnalyzeThrow(SyntaxNodeAnalysisContext context)
    {
        var namespaceName = context.ContainingSymbol?.ContainingNamespace?.ToDisplayString() ?? String.Empty;
        if (IsInCheckedLayer(namespaceName))
        {
            var throwKeyword = context.Node.GetFirstToken();
            context.ReportDiagnostic(Diagnostic.Create(Rule, throwKeyword.GetLocation(), namespaceName));
        }
    }

    private static Boolean IsInCheckedLayer(String namespaceName) =>
        namespaceName
            .Split('.')
            .Any(CheckedLayers.Contains);
}
