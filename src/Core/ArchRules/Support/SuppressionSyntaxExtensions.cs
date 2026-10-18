using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class SuppressionSyntaxExtensions
{
    private const String InactiveBranch = "in an inactive #if branch";

    private const String PragmaDisable = "#pragma warning disable";

    private const String NullableDisable = "#nullable disable";

    private const String SuppressMessage = "SuppressMessage";

    private static readonly String[] SuppressingAttributeNames = [SuppressMessage, "UnconditionalSuppressMessage"];

    extension(CompilationUnitSyntax source)
    {
        public IReadOnlyList<DirectiveTriviaSyntax> SuppressingDirectives =>
        [
            .. source
                .DescendantNodes(descendIntoTrivia: true)
                .OfType<DirectiveTriviaSyntax>()
                .Where(IsSuppressing),
        ];

        public IEnumerable<SourceFinding> FindSuppressions(IEnumerable<DirectiveTriviaSyntax> reportedDirectives) =>
        [
            .. reportedDirectives.Select(DescribeDirective),
            .. FindSuppressingAttributes(source),
            .. FindSuppressingAliases(source),
            .. FindSuppressionsInInactiveText(source),
        ];
    }

    private static Boolean IsSuppressing(DirectiveTriviaSyntax directive) =>
        directive switch
        {
            PragmaWarningDirectiveTriviaSyntax pragma => IsDisable(pragma.DisableOrRestoreKeyword),
            NullableDirectiveTriviaSyntax nullable => IsDisable(nullable.SettingToken),
            _ => false,
        };

    private static Boolean IsDisable(SyntaxToken setting) => setting.IsKind(SyntaxKind.DisableKeyword);

    private static IEnumerable<SourceFinding> FindSuppressingAttributes(CompilationUnitSyntax source) =>
        source
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => IsSuppressingName(attribute.Name))
            .Select(DescribeAttribute);

    private static IEnumerable<SourceFinding> FindSuppressingAliases(CompilationUnitSyntax source) =>
        source
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(IsSuppressingAlias)
            .Select(DescribeAlias);

    private static IEnumerable<SourceFinding> FindSuppressionsInInactiveText(CompilationUnitSyntax source) =>
        source
            .DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            .SelectMany(FindSuppressingLines);

    private static Boolean IsSuppressingAlias(UsingDirectiveSyntax usingDirective) =>
        usingDirective.Alias is not null && IsSuppressingName(usingDirective.NamespaceOrType);

    private static Boolean IsSuppressingName(TypeSyntax name) => SuppressingAttributeNames.Any(name.NamesAttribute);

    private static SourceFinding DescribeDirective(DirectiveTriviaSyntax directive) =>
        new(directive.GetLocation().StartLine, BuildDirectiveText(directive) + DescribeBranch(directive.IsActive));

    private static String BuildDirectiveText(DirectiveTriviaSyntax directive) =>
        directive switch
        {
            PragmaWarningDirectiveTriviaSyntax pragma => AppendArguments(PragmaDisable, BuildCodeList(pragma)),
            NullableDirectiveTriviaSyntax nullable => AppendArguments(NullableDisable, nullable.TargetToken.ValueText),
            _ => directive.ToString(),
        };

    private static String DescribeBranch(Boolean isActive) => isActive ? "" : $" {InactiveBranch}";

    private static String BuildCodeList(PragmaWarningDirectiveTriviaSyntax pragma) =>
        String.Join(", ", pragma.ErrorCodes);

    private static String AppendArguments(String directiveText, String arguments) =>
        arguments.Length == 0 ? directiveText : $"{directiveText} {arguments}";

    private static SourceFinding DescribeAttribute(AttributeSyntax attribute) =>
        new(attribute.GetLocation().StartLine, $"[{BuildTargetText(attribute)}{attribute.Name}]");

    private static String BuildTargetText(AttributeSyntax attribute) =>
        attribute.Parent is AttributeListSyntax { Target: { } target } ? $"{target.Identifier.ValueText}: " : "";

    private static SourceFinding DescribeAlias(UsingDirectiveSyntax alias) =>
        new(alias.GetLocation().StartLine, alias.ToString());

    private static IEnumerable<SourceFinding> FindSuppressingLines(SyntaxTrivia inactiveText)
    {
        var firstLine = inactiveText.GetLocation().StartLine;

        return inactiveText
            .ToFullString()
            .Split('\n')
            .Select((line, index) => new SourceFinding(firstLine + index, line.Trim()))
            .Where(finding => finding.Text.Contains(SuppressMessage, StringComparison.Ordinal))
            .Select(finding => finding with { Text = finding.Text + DescribeBranch(isActive: false) });
    }
}
