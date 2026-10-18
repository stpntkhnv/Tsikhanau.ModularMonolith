using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class MigrationSyntaxExtensions
{
    private const String NotDotnetEfOutput = "is not a migration, its Designer part or a model snapshot";

    private static readonly String[] ObsoleteWarningCodes = ["612", "618"];

    extension(CompilationUnitSyntax source)
    {
        public IEnumerable<SourceFinding> FindWhatDotnetEfDoesNotWrite()
        {
            var directives = source.SuppressingDirectives;
            var directivesDotnetEfWrites = GetDirectivesDotnetEfWrites(source, directives);

            return
            [
                .. source.FindSuppressions(directives.Except(directivesDotnetEfWrites)),
                .. FindTypesDotnetEfDoesNotWrite(source),
            ];
        }
    }

    private static IReadOnlyList<DirectiveTriviaSyntax> GetDirectivesDotnetEfWrites(
        CompilationUnitSyntax source,
        IReadOnlyList<DirectiveTriviaSyntax> directives) =>
    [
        .. directives
            .OfType<NullableDirectiveTriviaSyntax>()
            .Take(1)
            .Where(nullable => HasNoTarget(nullable) && IsLeading(source, nullable)),
        .. DeclaresADesignerPartOrSnapshot(source) ? GetFirstObsoleteWarningsDisable(directives) : [],
    ];

    private static IEnumerable<DirectiveTriviaSyntax> GetFirstObsoleteWarningsDisable(
        IReadOnlyList<DirectiveTriviaSyntax> directives) =>
        directives
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Where(DisablesObsoleteWarnings)
            .Take(1);

    private static IEnumerable<SourceFinding> FindTypesDotnetEfDoesNotWrite(CompilationUnitSyntax source) =>
    [
        .. source
            .DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(typeDeclaration => typeDeclaration is not ExtensionBlockDeclarationSyntax)
            .Where(typeDeclaration => !IsWrittenByDotnetEf(typeDeclaration))
            .Select(typeDeclaration => DescribeType(typeDeclaration.Identifier)),
        .. source
            .DescendantNodes()
            .OfType<DelegateDeclarationSyntax>()
            .Select(delegateDeclaration => DescribeType(delegateDeclaration.Identifier)),
    ];

    private static Boolean HasNoTarget(NullableDirectiveTriviaSyntax nullable) =>
        nullable.TargetToken.IsKind(SyntaxKind.None);

    private static Boolean IsLeading(CompilationUnitSyntax source, DirectiveTriviaSyntax directive) =>
        source.Members.FirstOrDefault() is { } firstMember && directive.SpanStart < firstMember.SpanStart;

    private static Boolean DisablesObsoleteWarnings(PragmaWarningDirectiveTriviaSyntax pragma) =>
        pragma.ErrorCodes
            .Select(code => code.ToString())
            .SequenceEqual(ObsoleteWarningCodes);

    private static Boolean DeclaresADesignerPartOrSnapshot(CompilationUnitSyntax source) =>
        source
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Any(classDeclaration => IsDesignerPart(classDeclaration) || IsModelSnapshot(classDeclaration));

    private static Boolean IsWrittenByDotnetEf(BaseTypeDeclarationSyntax typeDeclaration) =>
        typeDeclaration is ClassDeclarationSyntax classDeclaration
        && (IsMigration(classDeclaration) || IsDesignerPart(classDeclaration) || IsModelSnapshot(classDeclaration));

    private static Boolean IsMigration(ClassDeclarationSyntax classDeclaration) =>
        DerivesFrom(classDeclaration, "Migration");

    private static Boolean IsModelSnapshot(ClassDeclarationSyntax classDeclaration) =>
        DerivesFrom(classDeclaration, "ModelSnapshot");

    private static Boolean IsDesignerPart(ClassDeclarationSyntax classDeclaration) =>
        classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        && HasAttribute(classDeclaration, "DbContext")
        && HasAttribute(classDeclaration, "Migration");

    private static Boolean DerivesFrom(ClassDeclarationSyntax classDeclaration, String baseTypeName) =>
        classDeclaration.BaseList is { Types: [{ Type: var baseType }, ..] } && baseType.SimpleName == baseTypeName;

    private static Boolean HasAttribute(ClassDeclarationSyntax classDeclaration, String attributeName) =>
        classDeclaration.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Any(attribute => attribute.Name.NamesAttribute(attributeName));

    private static SourceFinding DescribeType(SyntaxToken identifier) =>
        new(identifier.GetLocation().StartLine, $"{identifier.ValueText} {NotDotnetEfOutput}");
}
