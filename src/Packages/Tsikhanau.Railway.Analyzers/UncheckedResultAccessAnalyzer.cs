using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Tsikhanau.Railway.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UncheckedResultAccessAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.UncheckedResultAccess);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RailwayTypes.Create(start.Compilation) is { } types)
            {
                start.RegisterOperationBlockAction(block => Analyze(block, types));
            }
        });
    }

    private static void Analyze(OperationBlockAnalysisContext context, RailwayTypes types)
    {
        foreach (var block in context.OperationBlocks)
        {
            if (!IsGraphRoot(block) || !AccessesValueOrError(block, types))
            {
                continue;
            }

            var graph = context.GetControlFlowGraph(block);
            new ResultFlowAnalysis(types, context.ReportDiagnostic).Analyze(graph, new ResultStates());
        }
    }

    private static Boolean IsGraphRoot(IOperation operation) => operation
        is IMethodBodyOperation
        or IConstructorBodyOperation
        or IFieldInitializerOperation
        or IPropertyInitializerOperation
        or IParameterInitializerOperation
        or IBlockOperation;

    private static Boolean AccessesValueOrError(IOperation block, RailwayTypes types) =>
        block.Descendants().Any(operation => operation is IPropertyReferenceOperation property
            && types.GetMember(property.Property) is ResultMember.Value or ResultMember.Error);
}
