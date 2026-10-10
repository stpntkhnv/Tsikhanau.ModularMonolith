using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatementSpacingAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0002",
            "Leave a blank line after a multi-line statement",
            "Leave a blank line after the multi-line statement above",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchSection, SyntaxKind.SwitchSection);
        context.RegisterSyntaxNodeAction(AnalyzeGlobalStatements, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context) =>
        Analyze(context, ((BlockSyntax)context.Node).Statements);

    private static void AnalyzeSwitchSection(SyntaxNodeAnalysisContext context) =>
        Analyze(context, ((SwitchSectionSyntax)context.Node).Statements);

    private static void AnalyzeGlobalStatements(SyntaxNodeAnalysisContext context) =>
        Analyze(context, GetGlobalStatements((CompilationUnitSyntax)context.Node));

    private static void Analyze(SyntaxNodeAnalysisContext context, IReadOnlyList<StatementSyntax> statements)
    {
        var crowdedPairs = statements
            .Zip(statements.Skip(1), (current, next) => (Current: current, Next: next))
            .Where(pair => IsMultiline(pair.Current) && !HasBlankLineBetween(pair.Current, pair.Next));

        foreach (var (_, next) in crowdedPairs)
        {
            var firstToken = next.GetFirstToken();
            context.ReportDiagnostic(Diagnostic.Create(Rule, firstToken.GetLocation()));
        }
    }

    private static IReadOnlyList<StatementSyntax> GetGlobalStatements(CompilationUnitSyntax compilationUnit) =>
        compilationUnit.Members
            .OfType<GlobalStatementSyntax>()
            .Select(globalStatement => globalStatement.Statement)
            .ToList();

    private static Boolean IsMultiline(StatementSyntax statement) =>
        statement.GetFirstToken().StartLine < statement.GetLastToken().EndLine;

    private static Boolean HasBlankLineBetween(StatementSyntax current, StatementSyntax next) =>
        next.GetFirstToken().StartLine - current.GetLastToken().EndLine > 1;
}
