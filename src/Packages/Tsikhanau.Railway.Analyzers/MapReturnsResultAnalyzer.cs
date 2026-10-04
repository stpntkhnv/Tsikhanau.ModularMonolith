using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Tsikhanau.Railway.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MapReturnsResultAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.MapReturnsResult);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RailwayTypes.Create(start.Compilation) is { } types)
            {
                start.RegisterOperationAction(operation => Analyze(operation, types), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, RailwayTypes types)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name is not ("Map" or "MapAsync") || !types.IsResultExtension(method))
        {
            return;
        }

        var returned = types.GetAwaitedType(method.ReturnType) ?? method.ReturnType;
        if (!types.IsNestedResult(returned))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.MapReturnsResult,
            GetNameLocation(invocation.Syntax),
            method.Name,
            method.Name == "Map" ? "Bind" : "BindAsync"));
    }

    private static Location GetNameLocation(SyntaxNode syntax)
    {
        var name = syntax is InvocationExpressionSyntax invocation
            ? invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
                SimpleNameSyntax simpleName => simpleName,
                _ => null
            }
            : null;

        return (name ?? syntax).GetLocation();
    }
}
