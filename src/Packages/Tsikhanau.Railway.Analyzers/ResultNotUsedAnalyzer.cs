using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Tsikhanau.Railway.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultNotUsedAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<String> TapMethods =
        ImmutableHashSet.Create("Tap", "TapAsync", "TapError", "TapErrorAsync");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.ResultNotUsed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RailwayTypes.Create(start.Compilation) is { } types)
            {
                start.RegisterOperationAction(operation => Analyze(operation, types), OperationKind.ExpressionStatement);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, RailwayTypes types)
    {
        var expression = ((IExpressionStatementOperation)context.Operation).Operation;
        if (expression is IAssignmentOperation
            || !types.IsResultOrAwaitableResult(expression.Type)
            || IsTapOnStoredResult(expression, types))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.ResultNotUsed,
            expression.Syntax.GetLocation(),
            Describe(expression)));
    }

    private static Boolean IsTapOnStoredResult(IOperation expression, RailwayTypes types)
    {
        if (expression is IAwaitOperation awaited)
        {
            expression = awaited.Operation;
        }
        else if (!types.IsResult(expression.Type))
        {
            return false;
        }

        return expression is IInvocationOperation && IsStoredOrTapped(expression, types);
    }

    private static Boolean IsStoredOrTapped(IOperation operation, RailwayTypes types) =>
        operation switch
        {
            ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation => true,
            IPropertyReferenceOperation property => property.Arguments.IsEmpty,
            IInvocationOperation invocation => TapMethods.Contains(invocation.TargetMethod.Name)
                && types.IsResultExtension(invocation.TargetMethod)
                && invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0) is { } source
                && IsStoredOrTapped(source.Value, types),
            _ => false
        };

    private static String Describe(IOperation expression)
    {
        var operation = expression is IAwaitOperation awaited ? awaited.Operation : expression;
        if (operation is IConditionalAccessOperation conditional)
        {
            operation = conditional.WhenNotNull;
        }

        return operation is IInvocationOperation invocation
            ? invocation.TargetMethod.Name
            : expression.Syntax.ToString();
    }
}
