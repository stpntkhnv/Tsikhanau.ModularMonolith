using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Tsikhanau.Railway.Analyzers.Rules;
using Tsikhanau.Railway.Analyzers.Symbols;

namespace Tsikhanau.Railway.Analyzers.ResultFlow;

internal sealed class ResultFlowAnalysis
{
    private const String AttributeNamespace = "System.Diagnostics.CodeAnalysis";

    private static readonly Action<IOperation, ResultStates> NoStep = static (_, _) => { };

    private readonly RailwayTypes _types;
    private readonly Action<Diagnostic> _report;

    public ResultFlowAnalysis(RailwayTypes types, Action<Diagnostic> report)
    {
        _types = types;
        _report = report;
    }

    public void ReportUncheckedAccesses(ControlFlowGraph graph, ResultStates entry)
    {
        var states = SolveBlockStates(graph, entry);
        ReportBlocks(graph, states);
        ReportLocalFunctions(graph);
    }

    private ResultStates?[] SolveBlockStates(ControlFlowGraph graph, ResultStates entry)
    {
        var blocks = graph.Blocks;
        var states = new ResultStates?[blocks.Length];
        var pending = new SortedSet<Int32> { 0 };
        states[0] = entry;

        for (var i = 1; i < blocks.Length; i++)
        {
            if (blocks[i].IsReachable && blocks[i].Predecessors.IsEmpty)
            {
                states[i] = new ResultStates();
                pending.Add(i);
            }
        }

        while (pending.Count > 0)
        {
            var ordinal = pending.Min;
            pending.Remove(ordinal);

            var block = blocks[ordinal];
            var state = states[ordinal]!.Clone();
            ApplyBlock(block, state);

            foreach (var (branch, branchState) in GetSuccessors(block, state))
            {
                if (branch?.Destination is { } destination && Merge(states, destination.Ordinal, branchState))
                {
                    pending.Add(destination.Ordinal);
                }
            }
        }

        return states;
    }

    private void ReportBlocks(ControlFlowGraph graph, ResultStates?[] states)
    {
        Action<IOperation, ResultStates> reportOperation =
            (operation, current) => ReportOperation(graph, operation, current);

        for (var i = 0; i < states.Length; i++)
        {
            if (states[i] is { } state)
            {
                WalkBlock(graph.Blocks[i], state.Clone(), reportOperation);
            }
        }
    }

    private void ReportLocalFunctions(ControlFlowGraph graph)
    {
        foreach (var localFunction in graph.LocalFunctions)
        {
            ReportUncheckedAccesses(graph.GetLocalFunctionControlFlowGraph(localFunction), new ResultStates());
        }
    }

    private static Boolean Merge(ResultStates?[] states, Int32 ordinal, ResultStates incoming)
    {
        if (incoming.IsUnreachable)
        {
            return false;
        }

        if (states[ordinal] is not { } current)
        {
            states[ordinal] = incoming.Clone();
            return true;
        }

        return current.IntersectWith(incoming);
    }

    private IEnumerable<(ControlFlowBranch? Branch, ResultStates State)> GetSuccessors(BasicBlock block, ResultStates state)
    {
        if (block.ConditionKind == ControlFlowConditionKind.None || block.BranchValue is null)
        {
            yield return (block.FallThroughSuccessor, state);
            yield break;
        }

        var whenTrue = state.Clone();
        Assume(block.BranchValue, true, whenTrue);

        var whenFalse = state.Clone();
        Assume(block.BranchValue, false, whenFalse);

        var conditionalIsTrue = block.ConditionKind == ControlFlowConditionKind.WhenTrue;
        yield return (block.ConditionalSuccessor, conditionalIsTrue ? whenTrue : whenFalse);
        yield return (block.FallThroughSuccessor, conditionalIsTrue ? whenFalse : whenTrue);
    }

    private void ApplyBlock(BasicBlock block, ResultStates state) => WalkBlock(block, state, NoStep);

    private void WalkBlock(BasicBlock block, ResultStates state, Action<IOperation, ResultStates> beforeEffect)
    {
        foreach (var operation in block.Operations)
        {
            Walk(operation, state, beforeEffect);
        }

        if (block.BranchValue is { } branchValue)
        {
            Walk(branchValue, state, beforeEffect);
        }
    }

    private void Walk(IOperation operation, ResultStates state, Action<IOperation, ResultStates> beforeEffect)
    {
        if (operation is IFlowAnonymousFunctionOperation)
        {
            beforeEffect(operation, state);
            return;
        }

        foreach (var child in operation.ChildOperations)
        {
            Walk(child, state, beforeEffect);
        }

        beforeEffect(operation, state);
        ApplyEffect(operation, state);
    }

    private void ReportOperation(ControlFlowGraph graph, IOperation operation, ResultStates state)
    {
        switch (operation)
        {
            case IFlowAnonymousFunctionOperation lambda when !state.IsUnreachable:
                ReportUncheckedAccesses(graph.GetAnonymousFunctionControlFlowGraph(lambda), state.Clone());
                break;
            case IPropertyReferenceOperation property:
                Check(property, state);
                break;
        }
    }

    private void ApplyEffect(IOperation operation, ResultStates state)
    {
        switch (operation)
        {
            case IDeconstructionAssignmentOperation:
                state.Clear();
                break;
            case IAssignmentOperation assignment:
                state.Forget(AccessPath.From(assignment.Target));
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                state.Forget(AccessPath.From(argument.Value));
                break;
            case IInvocationOperation invocation:
                ApplyContracts(invocation, state);
                break;
        }
    }

    private void Check(IPropertyReferenceOperation property, ResultStates state)
    {
        var member = _types.GetMember(property.Property);
        if (member is not (ResultMember.Value or ResultMember.Error)
            || state.IsUnreachable
            || property.Instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.PatternInput })
        {
            return;
        }

        var required = member == ResultMember.Value;
        var path = AccessPath.From(property.Instance);
        if (path is not null && (IsLambdaParameter(path.Root) || state.Get(path) == required))
        {
            return;
        }

        _report(Diagnostic.Create(
            Descriptors.UncheckedResultAccess,
            property.Syntax.GetLocation(),
            required ? "Value" : "Error",
            required ? "IsSuccess" : "IsFailure"));
    }

    private void ApplyContracts(IInvocationOperation invocation, ResultStates state)
    {
        if (HasAttribute(invocation.TargetMethod.GetAttributes(), "DoesNotReturnAttribute"))
        {
            state.MarkUnreachable();
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is { } parameter && GetDoesNotReturnIf(parameter) is { } stopsWhen)
            {
                Assume(argument.Value, !stopsWhen, state);
            }
        }
    }

    private void Assume(IOperation condition, Boolean value, ResultStates state)
    {
        switch (condition)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not:
                Assume(not.Operand, !value, state);
                break;
            case IPropertyReferenceOperation property:
                AssumeProperty(property.Instance, property.Property, value, state);
                break;
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binary:
                AssumeComparison(binary, value, state);
                break;
            case IIsPatternOperation isPattern:
                AssumePattern(isPattern.Value, isPattern.Pattern, value, state);
                break;
        }
    }

    private void AssumeComparison(IBinaryOperation binary, Boolean value, ResultStates state)
    {
        var holdsWhenEqual = (binary.OperatorKind == BinaryOperatorKind.Equals) == value;
        if (binary.RightOperand.ConstantValue is { HasValue: true, Value: Boolean right })
        {
            Assume(binary.LeftOperand, holdsWhenEqual ? right : !right, state);
        }
        else if (binary.LeftOperand.ConstantValue is { HasValue: true, Value: Boolean left })
        {
            Assume(binary.RightOperand, holdsWhenEqual ? left : !left, state);
        }
    }

    private void AssumePattern(IOperation input, IPatternOperation pattern, Boolean matched, ResultStates state)
    {
        switch (pattern)
        {
            case IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: Boolean expected } }:
                Assume(input, matched ? expected : !expected, state);
                break;
            case INegatedPatternOperation negated:
                AssumePattern(input, negated.Pattern, !matched, state);
                break;
            case IRecursivePatternOperation recursive
                when _types.IsResult(input.Type)
                && recursive.DeconstructionSubpatterns.IsEmpty
                && (matched || recursive.PropertySubpatterns.Length == 1):
                foreach (var subpattern in recursive.PropertySubpatterns)
                {
                    if (subpattern.Member is IPropertyReferenceOperation member)
                    {
                        AssumePropertyPattern(input, member.Property, subpattern.Pattern, matched, state);
                    }
                }

                break;
        }
    }

    private void AssumePropertyPattern(IOperation input, IPropertySymbol property, IPatternOperation pattern, Boolean matched, ResultStates state)
    {
        switch (pattern)
        {
            case IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: Boolean expected } }:
                AssumeProperty(input, property, matched ? expected : !expected, state);
                break;
            case INegatedPatternOperation negated:
                AssumePropertyPattern(input, property, negated.Pattern, !matched, state);
                break;
        }
    }

    private void AssumeProperty(IOperation? instance, IPropertySymbol property, Boolean value, ResultStates state)
    {
        var member = _types.GetMember(property);
        if (member is not (ResultMember.IsSuccess or ResultMember.IsFailure))
        {
            return;
        }

        if (AccessPath.From(instance) is { } path)
        {
            state.Set(path, (member == ResultMember.IsSuccess) == value);
        }
    }

    private static Boolean? GetDoesNotReturnIf(IParameterSymbol parameter)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (IsAttribute(attribute, "DoesNotReturnIfAttribute")
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is Boolean stopsWhen)
            {
                return stopsWhen;
            }
        }

        return null;
    }

    private static Boolean HasAttribute(IEnumerable<AttributeData> attributes, String name) =>
        attributes.Any(attribute => IsAttribute(attribute, name));

    private static Boolean IsAttribute(AttributeData attribute, String name) =>
        attribute.AttributeClass is { } type
        && type.Name == name
        && type.ContainingNamespace.ToDisplayString() == AttributeNamespace;

    private static Boolean IsLambdaParameter(ISymbol symbol) =>
        symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } };
}
