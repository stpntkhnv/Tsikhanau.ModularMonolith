using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.FixtureModules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

internal static class ModuleRuleCases
{
    private const String BadRoot = "Acme.Modules.Bad";

    private const String IntegrationEventTypeName = "Tsikhanau.Monolith.Contracts.IntegrationEvents.IntegrationEvent";

    private const String DomainEventTypeName = "Tsikhanau.Monolith.Domain.Events.DomainEvent";

    private const String ConfigurationTypeName = "Microsoft.Extensions.Configuration.IConfiguration";

    private const String LoggerTypeName = "Microsoft.Extensions.Logging.ILogger";

    private const String UnlistedKernelTypeName = "Acme.SharedKernel.Weight";

    private const String HttpContextTypeName = "Microsoft.AspNetCore.Http.HttpContext";

    private const String StrayServiceName = $"{BadRoot}.Features.StrayCrate.StrayDispatchService";

    private const String ModuleDispatcherTypeName = "Tsikhanau.Monolith.Infrastructure.Dispatching.ModuleDispatcher`1";

    private const String CommandHandlerTypeName = "Tsikhanau.Monolith.Application.Commands.ICommandHandler`2";

    private const String UnitTypeName = "Tsikhanau.Railway.Unit";

    private const String CrateRepositoryTypeName = $"{BadRoot}.Domain.Crates.ICrateRepository";

    private const String CrateSqlTypeName = $"{BadRoot}.Infrastructure.Persistence.CrateSql";

    private const String RenameCrateCommandTypeName = $"{BadRoot}.Features.RenameCrate.RenameCrateCommand";

    private const String RenameCrateHandlerTypeName =
        $"{CommandHandlerTypeName}[{RenameCrateCommandTypeName},{UnitTypeName}]";

    public static IEnumerable<RuleCase> All =>
    [
        .. DomainCases,
        .. StaticStateCases,
        .. VisibilityCases,
        .. NamingAndNullabilityCases,
        .. NamespaceOwnershipCases,
        .. LayerDependencyCases,
        .. HandlerBoundaryCases,
        .. SqlTextCases,
        .. NameCases,
        .. CoverageCases,
    ];

    private static IEnumerable<RuleCase> DomainCases =>
    [
        new(
            typeof(DomainRules),
            nameof(DomainRules.DomainTypesHaveNoPublicSetters),
            () => DomainRules.DomainTypesHaveNoPublicSetters(Bad),
            [$"{BadRoot}.Domain.Ledgers.Ledger.Count"],
            () => DomainRules.DomainTypesHaveNoPublicSetters(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.ValueObjectsCannotBeChangedEvenThroughInit),
            () => DomainRules.ValueObjectsCannotBeChangedEvenThroughInit(Bad),
            [$"{BadRoot}.Domain.Grades.Grade.Value"],
            () => DomainRules.ValueObjectsCannotBeChangedEvenThroughInit(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.ValueObjectsHideTheUncheckedFactory),
            () => DomainRules.ValueObjectsHideTheUncheckedFactory(Bad),
            [$"{BadRoot}.Domain.Scores.Score"],
            () => DomainRules.ValueObjectsHideTheUncheckedFactory(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.AggregatesEntitiesAndValueObjectsAreCreatedThroughTheirFactories),
            () => DomainRules.AggregatesEntitiesAndValueObjectsAreCreatedThroughTheirFactories(Bad),
            [$"{BadRoot}.Domain.Crates.Crate has a public constructor"],
            () => DomainRules.AggregatesEntitiesAndValueObjectsAreCreatedThroughTheirFactories(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.EventsCommandsAndQueriesAreImmutable),
            () => DomainRules.EventsCommandsAndQueriesAreImmutable(Bad),
            [$"{BadRoot}.Features.RenameCrate.RenameCrateCommand.Name"],
            () => DomainRules.EventsCommandsAndQueriesAreImmutable(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.AggregatesReferenceOtherAggregatesOnlyById),
            () => DomainRules.AggregatesReferenceOtherAggregatesOnlyById(Bad),
            [$"{BadRoot}.Domain.Pallets.Pallet holds {BadRoot}.Domain.Crates.Crate"],
            () => DomainRules.AggregatesReferenceOtherAggregatesOnlyById(Good)),
        new(
            typeof(DomainRules),
            nameof(DomainRules.DomainNeverReadsTheClockOrCreatesRandomIds),
            () => DomainRules.DomainNeverReadsTheClockOrCreatesRandomIds(Bad),
            [$"{BadRoot}.Domain.Stamps.Stamp::Now calls System.DateTimeOffset::get_UtcNow/0"],
            () => DomainRules.DomainNeverReadsTheClockOrCreatesRandomIds(Good)),
    ];

    private static IEnumerable<RuleCase> StaticStateCases =>
    [
        new(
            typeof(StaticStateRules),
            nameof(StaticStateRules.ModuleTypesHoldNoStaticMutableState),
            () => StaticStateRules.ModuleTypesHoldNoStaticMutableState(Bad),
            [$"{BadRoot}.Infrastructure.Counters.RequestCounter._count"],
            () => StaticStateRules.ModuleTypesHoldNoStaticMutableState(Good)),
        new(
            typeof(StaticStateRules),
            nameof(StaticStateRules.DomainTypesHaveNoPublicFields),
            () => StaticStateRules.DomainTypesHaveNoPublicFields(Bad),
            [$"{BadRoot}.Domain.Tallies.Tally.Count"],
            () => StaticStateRules.DomainTypesHaveNoPublicFields(Good)),
    ];

    private static IEnumerable<RuleCase> VisibilityCases =>
    [
        new(
            typeof(VisibilityRules),
            nameof(VisibilityRules.HandlersAreInternalAndSealed),
            () => VisibilityRules.HandlersAreInternalAndSealed(Bad),
            [$"{BadRoot}.Features.ArchiveCrate.ArchiveCrateCommandHandler"],
            () => VisibilityRules.HandlersAreInternalAndSealed(Good)),
        new(
            typeof(VisibilityRules),
            nameof(VisibilityRules.ModuleExposesAtMostItsModuleClassOutsideItsContracts),
            () => VisibilityRules.ModuleExposesAtMostItsModuleClassOutsideItsContracts(Bad),
            [$"{BadRoot}.Features.ExposeCrate.CrateExporter"],
            () => EvaluateEveryCleanModule(VisibilityRules.ModuleExposesAtMostItsModuleClassOutsideItsContracts)),
        new(
            typeof(VisibilityRules),
            nameof(VisibilityRules.ModuleAssemblyExposesOnlyItsModuleClass),
            () => VisibilityRules.ModuleAssemblyExposesOnlyItsModuleClass(Bad),
            [$"{BadRoot}.Features.ExposeCrate.CrateExporter"],
            () => EvaluateEveryCleanModule(VisibilityRules.ModuleAssemblyExposesOnlyItsModuleClass)),
        new(
            typeof(VisibilityRules),
            nameof(VisibilityRules.ContractsArePublic),
            () => VisibilityRules.ContractsArePublic(Bad),
            [$"{BadRoot}.Contracts.Queries.Hidden.HiddenCrateDto"],
            () => VisibilityRules.ContractsArePublic(Good)),
        new(
            typeof(VisibilityRules),
            nameof(VisibilityRules.EndpointsAreInternalAndSealed),
            () => VisibilityRules.EndpointsAreInternalAndSealed(Bad),
            [$"{BadRoot}.Features.ListCrates.ListCratesEndpoint"],
            () => VisibilityRules.EndpointsAreInternalAndSealed(Good)),
    ];

    private static IEnumerable<RuleCase> NamingAndNullabilityCases =>
    [
        new(
            typeof(NamingRules),
            nameof(NamingRules.TypesCarryTheSuffixOfTheirRole),
            () => NamingRules.TypesCarryTheSuffixOfTheirRole(Bad),
            [$"{BadRoot}.Domain.Crates.Events.CrateOpened should end with DomainEvent"],
            () => NamingRules.TypesCarryTheSuffixOfTheirRole(Good)),
        new(
            typeof(NullabilityRules),
            nameof(NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull),
            () => NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull(Bad),
            [$"{BadRoot}.Domain.Notes.NoteLookup.Find"],
            () => NullabilityRules.DomainAndFeatureClassesReturnOptionalInsteadOfNull(Good)),
        new(
            typeof(NullabilityRules),
            nameof(NullabilityRules.MessagesAndRequestsCarryOptionalInsteadOfNull),
            () => NullabilityRules.MessagesAndRequestsCarryOptionalInsteadOfNull(Bad),
            [$"{BadRoot}.Features.TagCrate.TagCrateCommand.Tag"],
            () => NullabilityRules.MessagesAndRequestsCarryOptionalInsteadOfNull(Good)),
    ];

    private static IEnumerable<RuleCase> NamespaceOwnershipCases =>
    [
        new(
            typeof(NamespaceOwnershipRules),
            nameof(NamespaceOwnershipRules.ModuleAssemblyHoldsOnlyTheModuleNamespaceOutsideItsContracts),
            () => NamespaceOwnershipRules.ModuleAssemblyHoldsOnlyTheModuleNamespaceOutsideItsContracts(Bad),
            ["Acme.Stray.Orphan"],
            () => NamespaceOwnershipRules.ModuleAssemblyHoldsOnlyTheModuleNamespaceOutsideItsContracts(Good)),
        new(
            typeof(NamespaceOwnershipRules),
            nameof(NamespaceOwnershipRules.ContractsAssemblyHoldsOnlyTheContractsNamespace),
            () => NamespaceOwnershipRules.ContractsAssemblyHoldsOnlyTheContractsNamespace(Bad),
            ["Acme.Elsewhere.Wanderer"],
            () => NamespaceOwnershipRules.ContractsAssemblyHoldsOnlyTheContractsNamespace(Good)),
    ];

    private static IEnumerable<RuleCase> LayerDependencyCases =>
    [
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway),
            () => LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(Dirty, Bad),
            [
                $"{BadRoot}.Domain.Leaks.EventLeak does depend on \"{IntegrationEventTypeName}\"",
                $"{BadRoot}.Domain.Journals.CrateJournal does depend on \"{LoggerTypeName}\"",
                $"{BadRoot}.Domain.Sizes.SizeRule does depend on \"{BadRoot}.Features.SizeCrate.CrateSize\"",
                $"{BadRoot}.Domain.Weighing.CrateScale does depend on \"{UnlistedKernelTypeName}\"",
            ],
            () => LayerDependencyRules.DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.DomainDoesNotDependOnFrameworksOutsideSystem),
            () => LayerDependencyRules.DomainDoesNotDependOnFrameworksOutsideSystem(Dirty, Bad),
            [$"{BadRoot}.Domain.Journals.CrateJournal does depend on \"{LoggerTypeName}\""],
            () => LayerDependencyRules.DomainDoesNotDependOnFrameworksOutsideSystem(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.FeaturesDoNotDependOnInfrastructure),
            () => LayerDependencyRules.FeaturesDoNotDependOnInfrastructure(Dirty, Bad),
            [$"{BadRoot}.Features.NameCrate.CrateNamer does depend on \"{BadRoot}.Infrastructure.Clocks.BadClock\""],
            () => LayerDependencyRules.FeaturesDoNotDependOnInfrastructure(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.FeaturesOtherThanEndpointsStayFreeOfWebAndDataFrameworks),
            () => LayerDependencyRules.FeaturesOtherThanEndpointsStayFreeOfWebAndDataFrameworks(Dirty, Bad),
            [
                $"{BadRoot}.Features.ReadRequest.RequestReader does depend on \"{HttpContextTypeName}\"",
                $"{BadRoot}.Features.DispatchCrate.CrateDispatchService does depend on \"{ModuleDispatcherTypeName}\"",
                $"{StrayServiceName} does depend on \"{ModuleDispatcherTypeName}\"",
            ],
            () => LayerDependencyRules.FeaturesOtherThanEndpointsStayFreeOfWebAndDataFrameworks(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.EndpointsGoThroughTheFacade),
            () => LayerDependencyRules.EndpointsGoThroughTheFacade(Dirty, Bad),
            [$"{BadRoot}.Features.ShowCrate.ShowCrateEndpoint does depend on \"{CrateRepositoryTypeName}\""],
            () => LayerDependencyRules.EndpointsGoThroughTheFacade(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.SettingsDependOnNothingElseInTheModule),
            () => LayerDependencyRules.SettingsDependOnNothingElseInTheModule(Dirty, Bad),
            [$"{BadRoot}.Settings.BadSettings does depend on \"{BadRoot}.Features.SizeCrate.CrateSize\""],
            () => LayerDependencyRules.SettingsDependOnNothingElseInTheModule(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.InfrastructureDoesNotDependOnEndpointsOrHttp),
            () => LayerDependencyRules.InfrastructureDoesNotDependOnEndpointsOrHttp(Dirty, Bad),
            [$"{BadRoot}.Infrastructure.Routing.RouteBinder does depend on \"{BadRoot}.Http.CrateRoutes\""],
            () => LayerDependencyRules.InfrastructureDoesNotDependOnEndpointsOrHttp(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway),
            () => LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(Dirty, Bad),
            [
                $"{BadRoot}.Contracts.Queries.CrateHistory.CrateHistoryDto does depend on \"{DomainEventTypeName}\"",
                $"{BadRoot}.Contracts.Queries.CrateWeight.CrateWeightDto does depend on \"{UnlistedKernelTypeName}\"",
            ],
            () => LayerDependencyRules.ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.ModuleReachesOtherModulesOnlyThroughTheirContracts),
            () => LayerDependencyRules.ModuleReachesOtherModulesOnlyThroughTheirContracts(Dirty, Bad),
            [$"{BadRoot}.Infrastructure.Neighbours.PeerProbe does depend on \"Acme.Modules.Peer.PeerModule\""],
            () => LayerDependencyRules.ModuleReachesOtherModulesOnlyThroughTheirContracts(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.ModuleDoesNotReadConfigurationDirectly),
            () => LayerDependencyRules.ModuleDoesNotReadConfigurationDirectly(Dirty, Bad),
            [$"{BadRoot}.Infrastructure.Configuration.SettingsReader does depend on \"{ConfigurationTypeName}\""],
            () => LayerDependencyRules.ModuleDoesNotReadConfigurationDirectly(Clean, Good)),
        new(
            typeof(LayerDependencyRules),
            nameof(LayerDependencyRules.ModuleDoesNotOpenItsOwnDatabaseConnections),
            () => LayerDependencyRules.ModuleDoesNotOpenItsOwnDatabaseConnections(Dirty, Bad),
            [$"{BadRoot}.Infrastructure.Connections.ConnectionOpener does depend on \"Npgsql.NpgsqlConnection\""],
            () => LayerDependencyRules.ModuleDoesNotOpenItsOwnDatabaseConnections(Clean, Good)),
    ];

    private static IEnumerable<RuleCase> HandlerBoundaryCases =>
    [
        new(
            typeof(HandlerBoundaryRules),
            nameof(HandlerBoundaryRules.ModuleTakesNoHandlerInAConstructor),
            () => HandlerBoundaryRules.ModuleTakesNoHandlerInAConstructor(Bad),
            [$"{BadRoot}.Infrastructure.Relays.CrateRelay takes {RenameCrateHandlerTypeName} handler"],
            () => HandlerBoundaryRules.ModuleTakesNoHandlerInAConstructor(Good)),
        new(
            typeof(HandlerBoundaryRules),
            nameof(HandlerBoundaryRules.QueryMarkerIsImplementedOnlyInsideItsModule),
            () => HandlerBoundaryRules.QueryMarkerIsImplementedOnlyInsideItsModule(Dirty, Peer),
            [$"{BadRoot}.Features.StealQuery.StolenPeerQuery"],
            () => HandlerBoundaryRules.QueryMarkerIsImplementedOnlyInsideItsModule(Clean, Peer)),
        new(
            typeof(HandlerBoundaryRules),
            nameof(HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies),
            () => HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies(Bad),
            [
                $"{BadRoot}.Features.CountCrates.CountCratesQueryHandler takes IOutbox`1 outbox",
                $"{BadRoot}.Features.CountCrates.CountCratesQueryHandler holds IOutbox`1 <outbox>P",
            ],
            () => HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies(Good)),
        new(
            typeof(HandlerBoundaryRules),
            nameof(HandlerBoundaryRules.OnlyEndpointsAndBackgroundJobsTakeTheModuleFacade),
            () => HandlerBoundaryRules.OnlyEndpointsAndBackgroundJobsTakeTheModuleFacade(Bad),
            [
                $"{BadRoot}.Features.LoopBack.LoopBackService takes IBadModule badModule",
                $"{BadRoot}.Features.LoopBack.LoopBackService holds IBadModule <badModule>P",
                $"{BadRoot}.Features.DispatchCrate.CrateDispatchService takes ModuleDispatcher`1 moduleDispatcher",
                $"{BadRoot}.Features.DispatchCrate.CrateDispatchService holds ModuleDispatcher`1 <moduleDispatcher>P",
                $"{StrayServiceName} takes ModuleDispatcher`1 strayDispatcher",
                $"{StrayServiceName} holds ModuleDispatcher`1 <strayDispatcher>P",
                $"{BadRoot}.Features.KeyCrate.CrateKeyService takes Keyed`1 keyedModule",
                $"{BadRoot}.Features.KeyCrate.CrateKeyService holds Keyed`1 <keyedModule>P",
            ],
            () => HandlerBoundaryRules.OnlyEndpointsAndBackgroundJobsTakeTheModuleFacade(Good)),
    ];

    private static IEnumerable<RuleCase> SqlTextCases =>
    [
        new(
            typeof(SqlTextRules),
            nameof(SqlTextRules.QueryHandlersOnlyRead),
            () => SqlTextRules.QueryHandlersOnlyRead(Bad),
            [$"{BadRoot}.Features.TouchCrates.TouchCratesQueryHandler writes: UPDATE bad.crates SET touched = now()"],
            () => SqlTextRules.QueryHandlersOnlyRead(Good)),
        new(
            typeof(SqlTextRules),
            nameof(SqlTextRules.ModuleSqlNamesNoOtherModuleSchema),
            () => SqlTextRules.ModuleSqlNamesNoOtherModuleSchema(Dirty, Bad),
            [$"{BadRoot}.Infrastructure.PeerReads.PeerNameSql reaches another module: SELECT name FROM peer.peers"],
            () => SqlTextRules.ModuleSqlNamesNoOtherModuleSchema(Clean, Good)),
        new(
            typeof(SqlTextRules),
            nameof(SqlTextRules.ModuleSqlNamesItsColumns),
            () => SqlTextRules.ModuleSqlNamesItsColumns(Bad),
            [
                $"{CrateSqlTypeName} reads every column: SELECT * FROM bad.crates",
                $"{CrateSqlTypeName} reads every column: SELECT count(*), c.* FROM bad.crates c",
            ],
            () => SqlTextRules.ModuleSqlNamesItsColumns(Good)),
    ];

    private static IEnumerable<RuleCase> NameCases =>
    [
        new(
            typeof(IntegrationEventNameRules),
            nameof(IntegrationEventNameRules.EveryIntegrationEventHasAUniqueStableName),
            () => IntegrationEventNameRules.EveryIntegrationEventHasAUniqueStableName(Dirty),
            [
                $"{BadRoot}.Contracts.IntegrationEvents.CrateEmptiedIntegrationEvent has no [IntegrationEventName]",
                "peer.peer-joined names more than one integration event",
            ],
            () => IntegrationEventNameRules.EveryIntegrationEventHasAUniqueStableName(Clean)),
        new(
            typeof(InternalCommandNameRules),
            nameof(InternalCommandNameRules.EveryInternalCommandHasAStableNameUniqueInItsModule),
            () => InternalCommandNameRules.EveryInternalCommandHasAStableNameUniqueInItsModule(Dirty),
            [
                $"{BadRoot}.Features.SealCrate.SealCrateInternalCommand has no [InternalCommandName] with a name",
                "stack-crate names more than one internal command of Bad",
            ],
            () => InternalCommandNameRules.EveryInternalCommandHasAStableNameUniqueInItsModule(Clean)),
    ];

    private static IEnumerable<RuleCase> CoverageCases =>
    [
        new(
            typeof(CoverageRules),
            nameof(CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts),
            () => CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts(BadWithoutItsContracts),
            [$"Bad does not load {BadRoot}.Contracts"],
            () => CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts(Good)),
    ];

    private static ArchModule BadWithoutItsContracts =>
        new(Bad.Name, Bad.RootNamespace, () => [FixtureAssemblies.Get(Bad.RootNamespace)]);

    private static RuleEvaluation EvaluateEveryCleanModule(Func<ArchModule, RuleEvaluation> rule)
    {
        IReadOnlyList<RuleEvaluation> evaluations = [.. Clean.All.Select(rule)];

        return new(
            [.. evaluations.SelectMany(evaluation => evaluation.Subject)],
            [.. evaluations.SelectMany(evaluation => evaluation.Violations)]);
    }
}
