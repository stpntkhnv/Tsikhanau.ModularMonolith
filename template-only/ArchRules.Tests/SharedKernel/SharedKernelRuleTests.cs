using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Projects;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.FixtureModules;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.ScratchTree;

namespace Tsikhanau.Monolith.ArchRules.Tests.SharedKernel;

public sealed class SharedKernelRuleTests : ArchRuleTests<FixtureInputs>
{
    private const String MoneyTypeName = "Acme.SharedKernel.Money";

    private const String WidgetDependsOnMoney =
        $"Acme.Modules.Good.Domain.Widgets.Widget does depend on \"{MoneyTypeName}\"";

    private const String WidgetDtoDependsOnMoney =
        $"Acme.Modules.Good.Contracts.Queries.GetWidget.WidgetDto does depend on \"{MoneyTypeName}\"";

    private static readonly ArchModules WithoutTheKernel = new([Good, Peer]);

    private static ArchModule GoodLoadingTheKernel =>
        new(Good.Name, Good.RootNamespace, () => [.. Good.Assemblies, Kernel]);

    [Fact]
    public void Domain_may_depend_on_the_kernel_the_inputs_list()
    {
        var evaluation = LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(Modules, Good);

        evaluation.ShouldBeSilent();
    }

    [Fact]
    public void Contracts_may_depend_on_the_kernel_the_inputs_list()
    {
        var evaluation = LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(Modules, Good);

        evaluation.ShouldBeSilent();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Domain_may_not_depend_on_a_kernel_missing_from_the_list(Boolean isKernelLoaded)
    {
        var good = isKernelLoaded ? GoodLoadingTheKernel : Good;

        var evaluation = LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(
            new ArchModules([good, Peer]),
            good);

        evaluation.ShouldFindExactly([WidgetDependsOnMoney]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Contracts_may_not_depend_on_a_kernel_missing_from_the_list(Boolean isKernelLoaded)
    {
        var good = isKernelLoaded ? GoodLoadingTheKernel : Good;

        var evaluation = LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(
            new ArchModules([good, Peer]),
            good);

        evaluation.ShouldFindExactly([WidgetDtoDependsOnMoney]);
    }

    [Fact]
    public void Project_references_may_name_the_kernel_the_inputs_list()
    {
        var evaluation = Evaluate(
            solution => ProjectGraphRules.ProjectReferencesFollowTheLayerRules(solution, Modules, Good));

        evaluation.ShouldBeSilent();
    }

    [Fact]
    public void Project_references_may_not_name_a_kernel_missing_from_the_list()
    {
        var evaluation = Evaluate(
            solution => ProjectGraphRules.ProjectReferencesFollowTheLayerRules(solution, WithoutTheKernel, Good));

        evaluation.ShouldFindExactly(
            [
                "Acme.Modules.Good references Acme.SharedKernel",
                "Acme.Modules.Good.Contracts references Acme.SharedKernel",
            ]);
    }
}
