using Shouldly;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.Testing.Modules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Rules;

public abstract class ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    public static TheoryData<String> ModuleNames =>
        Modules.All.Count == 0
            ? [ProductionModules.Placeholder]
            : [.. Modules.All.Select(module => module.Name)];

    protected static ArchModules Modules { get; } = new(TInputs.Modules, TInputs.SharedKernelAssemblies);

    protected static ArchSolution Solution { get; } = new(TInputs.SolutionRoot, TInputs.RootNamespace);

    protected static ArchModule GetModule(String name) =>
        name == ProductionModules.Placeholder ? new ArchModule(name, name, () => []) : Modules.Get(name);

    protected static void Check(RuleEvaluation evaluation, String noSubjectReason)
    {
        if (HasSubjectToCheck(evaluation, noSubjectReason))
        {
            evaluation.Violations.ShouldBeEmpty();
        }
    }

    protected static void Check(RuleEvaluation evaluation, String noSubjectReason, String advice)
    {
        if (HasSubjectToCheck(evaluation, noSubjectReason))
        {
            evaluation.Violations.ShouldBeEmpty(advice);
        }
    }

    private static Boolean HasSubjectToCheck(RuleEvaluation evaluation, String noSubjectReason)
    {
        if (evaluation.Subject.Count > 0)
        {
            return true;
        }

        Assert.SkipWhen(TInputs.EmptySubject == EmptySubject.Skip, noSubjectReason);
        return false;
    }
}
