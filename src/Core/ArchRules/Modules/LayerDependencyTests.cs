using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class LayerDependencyTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    private const String DomainAllowList =
        "System, its own Domain, Tsikhanau.Monolith.Domain, Tsikhanau.Monolith.Shared, Railway";

    private const String DomainAdvice =
        $"Domain sees only {DomainAllowList} and the app's listed shared kernel assemblies";

    [Theory]
    [EveryModule]
    public void Domain_depends_only_on_itself_the_domain_building_blocks_and_railway(String name) =>
        Check(
            LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(Modules, GetModule(name)),
            $"{name} has no types in Domain",
            DomainAdvice);

    [Theory]
    [EveryModule]
    public void Domain_does_not_depend_on_frameworks_outside_system(String name) =>
        Check(
            LayerDependencyRules.DomainDoesNotDependOnFrameworksOutsideSystem(Modules, GetModule(name)),
            $"{name} has no types in Domain",
            DomainAdvice);

    [Theory]
    [EveryModule]
    public void Features_do_not_depend_on_infrastructure(String name) =>
        Check(
            LayerDependencyRules.FeaturesDoNotDependOnInfrastructure(Modules, GetModule(name)),
            $"{name} has no types in Features",
            "Declare the interface in Domain or Features and implement it in Infrastructure");

    [Theory]
    [EveryModule]
    public void Features_other_than_endpoints_stay_free_of_web_and_data_frameworks(String name) =>
        Check(
            LayerDependencyRules.FeaturesOtherThanEndpointsStayFreeOfWebAndDataFrameworks(Modules, GetModule(name)),
            $"{name} has no types in Features",
            "Keep ASP.NET Core and EF Core in endpoints and Infrastructure; a handler works through interfaces");

    [Theory]
    [EveryModule]
    public void Endpoints_go_through_the_facade(String name) =>
        Check(
            LayerDependencyRules.EndpointsGoThroughTheFacade(Modules, GetModule(name)),
            $"{name} has no endpoints",
            "Send the command or query through the module facade, not a handler or a repository");

    [Theory]
    [EveryModule]
    public void Settings_depend_on_nothing_else_in_the_module(String name) =>
        Check(
            LayerDependencyRules.SettingsDependOnNothingElseInTheModule(Modules, GetModule(name)),
            $"{name} has no types in Settings",
            "Keep the settings record plain: built-in types and data annotations only");

    [Theory]
    [EveryModule]
    public void Infrastructure_does_not_depend_on_endpoints_or_http(String name) =>
        Check(
            LayerDependencyRules.InfrastructureDoesNotDependOnEndpointsOrHttp(Modules, GetModule(name)),
            $"{name} has no types in Infrastructure",
            "Keep HTTP in endpoints and pass Infrastructure plain values");

    [Theory]
    [EveryModule]
    public void Contracts_depend_only_on_the_contract_building_blocks_and_railway(String name) =>
        Check(
            LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(Modules, GetModule(name)),
            $"{name} has no types in Contracts",
            "Keep Contracts to plain records on the contract building blocks; move the rest into the module");

    [Theory]
    [EveryModule]
    public void Module_reaches_other_modules_only_through_their_contracts(String name) =>
        Check(
            LayerDependencyRules.ModuleReachesOtherModulesOnlyThroughTheirContracts(Modules, GetModule(name)),
            $"{name} has no types",
            "Reference the neighbour's Contracts and call its facade or handle its integration events");

    [Theory]
    [EveryModule]
    public void Module_does_not_read_configuration_directly(String name) =>
        Check(
            LayerDependencyRules.ModuleDoesNotReadConfigurationDirectly(Modules, GetModule(name)),
            $"{name} has no types",
            "Bind a settings record with AddSettings<TSettings> and take it in the constructor");

    [Theory]
    [EveryModule]
    public void Module_does_not_open_its_own_database_connections(String name) =>
        Check(
            LayerDependencyRules.ModuleDoesNotOpenItsOwnDatabaseConnections(Modules, GetModule(name)),
            $"{name} has no types",
            "Take ISqlConnectionFactory or the module's DbContext instead of opening a connection");
}
