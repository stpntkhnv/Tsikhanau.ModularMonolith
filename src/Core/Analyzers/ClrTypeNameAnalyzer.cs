using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClrTypeNameAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0004",
            "Use the CLR type name instead of the C# keyword",
            "Use the CLR type name '{0}' instead of '{1}'",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private static readonly ImmutableDictionary<SyntaxKind, String> ClrNames =
        ImmutableDictionary.CreateRange(
        [
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.BoolKeyword, "Boolean"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.ByteKeyword, "Byte"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.SByteKeyword, "SByte"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.CharKeyword, "Char"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.DecimalKeyword, "Decimal"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.DoubleKeyword, "Double"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.FloatKeyword, "Single"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.IntKeyword, "Int32"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.UIntKeyword, "UInt32"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.LongKeyword, "Int64"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.ULongKeyword, "UInt64"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.ShortKeyword, "Int16"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.UShortKeyword, "UInt16"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.ObjectKeyword, "Object"),
            new KeyValuePair<SyntaxKind, String>(SyntaxKind.StringKeyword, "String"),
        ]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeKeyword, SyntaxKind.PredefinedType);
    }

    private static void AnalyzeKeyword(SyntaxNodeAnalysisContext context)
    {
        var keyword = ((PredefinedTypeSyntax)context.Node).Keyword;
        if (ClrNames.TryGetValue(keyword.Kind(), out var clrName))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), clrName, keyword.Text));
        }
    }
}
