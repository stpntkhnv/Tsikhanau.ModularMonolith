using System.Runtime.CompilerServices;
using Shouldly;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.Testing.Modules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Rules;

public abstract class ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    public static TheoryData<String> ModuleNames =>
        ProductionModules.CreateRows([.. Modules.All.Select(module => module.Name)]);

    protected static ArchModules Modules { get; } = new(TInputs.Modules, TInputs.SharedKernelAssemblies);

    protected static ArchSolution Solution { get; } = new(TInputs.SolutionRoot, TInputs.RootNamespace);

    private static Boolean SkipsEmptySubject { get; } =
        typeof(TInputs).IsAssignableTo(typeof(IAppArchRuleInputs)) || TInputs.EmptySubject == EmptySubject.Skip;

    protected static ArchModule GetModule(String name) => Modules.Get(name);

    protected static void Check(
        RuleEvaluation evaluation,
        String noSubjectReason,
        String fix,
        [CallerMemberName] String test = "")
    {
        if (HasSubjectToCheck(evaluation, noSubjectReason))
        {
            evaluation.Violations.ShouldBeEmpty(RuleLines.Explain(test, fix));
        }
    }

    private static Boolean HasSubjectToCheck(RuleEvaluation evaluation, String noSubjectReason)
    {
        if (evaluation.Subject.Count > 0)
        {
            return true;
        }

        Assert.SkipWhen(SkipsEmptySubject, noSubjectReason);
        return false;
    }
}
