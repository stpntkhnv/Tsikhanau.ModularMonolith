using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LineLengthAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0008",
            "Fit the line in 120 characters",
            "Line is {0} characters long, more than 120: wrap it at the highest level first (STYLE.md 9), name a "
            + "long string or split it into parts joined by `+`",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private const Int32 MaxLength = 120;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var longLines = context.Tree
            .GetText(context.CancellationToken)
            .Lines
            .Where(line => line.Span.Length > MaxLength && !IsRawStringData(root, line));

        foreach (var line in longLines)
        {
            var location = Location.Create(context.Tree, line.Span);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, line.Span.Length));
        }
    }

    private static Boolean IsRawStringData(SyntaxNode root, TextLine line)
    {
        var token = root.FindToken(line.Start);

        return token.Span.Contains(line.Span) && IsInnerLineOfRawString(token, line);
    }

    private static Boolean IsInnerLineOfRawString(SyntaxToken token, TextLine line) =>
        token.Kind() switch
        {
            SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken =>
                IsInnerLine(token.Span, line),
            SyntaxKind.InterpolatedStringTextToken =>
                token.Parent?.Parent is InterpolatedStringExpressionSyntax literal
                && literal.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken)
                && IsInnerLine(literal.Span, line),
            _ => false,
        };

    private static Boolean IsInnerLine(TextSpan literal, TextLine line) =>
        literal.Start < line.Start && line.EndIncludingLineBreak < literal.End;
}
