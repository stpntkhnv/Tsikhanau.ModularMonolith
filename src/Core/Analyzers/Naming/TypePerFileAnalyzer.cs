using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Tsikhanau.Monolith.Analyzers.Rules;

namespace Tsikhanau.Monolith.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypePerFileAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0009",
            "Keep one type per file",
            "Type {0} does not belong in {1}: one type per file, named after the type, so move it to {2}",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        if (context.Tree.FilePath.Length == 0)
        {
            return;
        }

        var file = Path.GetFileName(context.Tree.FilePath);
        var fileStem = GetFileStem(file);

        foreach (var type in GetTypesOutOfPlace(ReadTopLevelTypes(context), fileStem))
        {
            ReportTypeOutOfPlace(context, type, file, fileStem);
        }
    }

    private static String GetFileStem(String file) => file.Split('.')[0];

    private static IReadOnlyList<TopLevelType> ReadTopLevelTypes(SyntaxTreeAnalysisContext context)
    {
        var members = context.Tree
            .GetRoot(context.CancellationToken)
            .ChildNodes();

        return CollectTypes(members, String.Empty)
            .ToList();
    }

    private static IEnumerable<TopLevelType> GetTypesOutOfPlace(IReadOnlyList<TopLevelType> types, String fileStem)
    {
        var ownNamespace = types
            .Where(type => type.Name == fileStem)
            .Select(type => type.Namespace)
            .FirstOrDefault();

        return types.Where(type => type.Name != fileStem || type.Namespace != ownNamespace);
    }

    private static void ReportTypeOutOfPlace(
        SyntaxTreeAnalysisContext context,
        TopLevelType type,
        String file,
        String fileStem)
    {
        var place = DescribePlace(type, fileStem);
        context.ReportDiagnostic(Diagnostic.Create(Rule, type.Identifier.GetLocation(), type.Name, file, place));
    }

    private static String DescribePlace(TopLevelType type, String fileStem) =>
        type.Name == fileStem ? DescribeOwnFile(type) : $"{type.Name}.cs";

    private static String DescribeOwnFile(TopLevelType type) =>
        type.Namespace.Length == 0
            ? $"a {type.Name}.cs of its own in the folder of the global namespace"
            : $"a {type.Name}.cs of its own in the folder of namespace {type.Namespace}";

    private static IEnumerable<TopLevelType> CollectTypes(IEnumerable<SyntaxNode> members, String currentNamespace)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax declaration:
                    var inner = currentNamespace.Length == 0
                        ? declaration.Name.ToString()
                        : $"{currentNamespace}.{declaration.Name}";

                    foreach (var type in CollectTypes(declaration.Members, inner))
                    {
                        yield return type;
                    }

                    break;
                case BaseTypeDeclarationSyntax declaration when !IsFileLocal(declaration.Modifiers):
                    yield return new(declaration.Identifier, currentNamespace);

                    break;
                case DelegateDeclarationSyntax declaration when !IsFileLocal(declaration.Modifiers):
                    yield return new(declaration.Identifier, currentNamespace);

                    break;
            }
        }
    }

    private static Boolean IsFileLocal(SyntaxTokenList modifiers) => modifiers.Any(SyntaxKind.FileKeyword);

    private sealed class TopLevelType(SyntaxToken identifier, String declaredNamespace)
    {
        public SyntaxToken Identifier { get; } = identifier;

        public String Namespace { get; } = declaredNamespace;

        public String Name => Identifier.ValueText;
    }
}
