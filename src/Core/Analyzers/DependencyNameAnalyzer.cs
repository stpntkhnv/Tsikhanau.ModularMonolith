using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependencyNameAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0003",
            "Name the dependency after its type",
            "Name the dependency after its type: '{0}'",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private static readonly ImmutableHashSet<(String TypeName, String ParameterName)> FrameworkNames =
        ImmutableHashSet.Create(
            ("WebApplication", "app"),
            ("IApplicationBuilder", "app"),
            ("IEndpointRouteBuilder", "endpoints"),
            ("IServiceCollection", "services"));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeConstructor, SymbolKind.Method);
    }

    private static void AnalyzeConstructor(SymbolAnalysisContext context)
    {
        var constructor = (IMethodSymbol)context.Symbol;
        if (!IsDependencyConstructor(constructor))
        {
            return;
        }

        foreach (var parameter in constructor.Parameters.Where(IsDependency))
        {
            var expectedName = GetExpectedName(parameter.Type);
            if (!IsNamedAfter(parameter.Name, expectedName) && !HasFrameworkName(parameter))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations[0], ToCamelCase(expectedName)));
            }
        }
    }

    private static Boolean IsDependencyConstructor(IMethodSymbol constructor) =>
        constructor.MethodKind == MethodKind.Constructor
        && !constructor.IsImplicitlyDeclared
        && constructor.DeclaredAccessibility != Accessibility.Private
        && constructor.ContainingType is { TypeKind: TypeKind.Class, IsRecord: false };

    private static Boolean IsDependency(IParameterSymbol parameter) =>
        parameter.Type is INamedTypeSymbol type
        && type.TypeKind is TypeKind.Interface or TypeKind.Class
        && !type.IsRecord
        && !IsCollection(type);

    private static Boolean IsCollection(INamedTypeSymbol type) =>
        IsEnumerable(type) || type.AllInterfaces.Any(IsEnumerable);

    private static Boolean IsEnumerable(INamedTypeSymbol type) =>
        type.SpecialType == SpecialType.System_Collections_IEnumerable;

    private static Boolean HasFrameworkName(IParameterSymbol parameter) =>
        FrameworkNames.Contains((parameter.Type.Name, parameter.Name));

    private static String GetExpectedName(ITypeSymbol type) =>
        HasInterfacePrefix(type) ? type.Name.Substring(1) : type.Name;

    private static Boolean HasInterfacePrefix(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Interface
        && type.Name.Length > 1
        && type.Name[0] == 'I'
        && Char.IsUpper(type.Name[1]);

    private static Boolean IsNamedAfter(String name, String expectedName) =>
        IsCamelCase(name) && (name == ToCamelCase(expectedName) || HasQualifierBefore(name, expectedName));

    private static Boolean IsCamelCase(String name) =>
        Char.IsLower(name[0]);

    private static Boolean HasQualifierBefore(String name, String expectedName) =>
        name.Length > expectedName.Length && name.EndsWith(expectedName, StringComparison.Ordinal);

    private static String ToCamelCase(String name) =>
        Char.ToLowerInvariant(name[0]) + name.Substring(1);
}
