using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Projects;

namespace Tsikhanau.Monolith.ArchRules.Rules;

internal static class RuleLines
{
    private const String OptionalInsteadOfNull =
        "Absence is `Optional<T>`, never `null`: in what Domain types and feature classes return and in what "
        + "integration events, commands and request records carry. Queries and DTOs carry `Optional<T>` too; only a "
        + "Dapper `<Name>Row` record holds a nullable member, which its handler maps to the DTO at once.";

    private const String InjectedTime =
        "Time comes from the injected `TimeProvider`, ids from `Guid.CreateVersion7(createdOn)`; the domain takes "
        + "the moment as a parameter.";

    private const String NeighboursThroughContracts =
        "A module reaches a neighbour only through the neighbour's Contracts, and modules never reference each "
        + "other in a cycle.";

    private const String Layers =
        "Domain depends only on itself and the domain building blocks; Features never depend on Infrastructure; "
        + "endpoints go only through the facade; an endpoint holds no branching.";

    private const String Settings =
        "A module reads its settings as a validated record bound with `AddSettings<TSettings>`, never through "
        + "`IConfiguration`.";

    private const String ModelThroughFactories =
        "Aggregates, entities and value objects are made only through their factories, have no public setters and "
        + "reference other aggregates only by id.";

    private const String QueriesOnlyRead =
        "A query only reads: its handler takes no write side and its SQL writes nothing.";

    private const String ModuleSql =
        "Module SQL names its columns and never another module's schema; module code opens no database connection "
        + "of its own. SQL passed to Dapper or EF Core raw SQL is a constant, values go in parameters.";

    private const String Visibility =
        "Handlers and endpoints are `internal sealed`; outside its Contracts a module exposes only its module class.";

    private const String StableNames =
        "Every integration event has a stable `[IntegrationEventName]` unique in the app, every internal command a "
        + "stable `[InternalCommandName]` unique in its module.";

    private const String BuildSettingsInTheCore =
        "Build and analyzer settings live only in the core: no `.editorconfig`, `Directory.Build.*` or "
        + "`Directory.Packages.props` anywhere else, and no `#pragma warning disable` or `[SuppressMessage]` to "
        + "quiet a diagnostic: fix the code.";

    private static readonly RuleLine[] All =
    [
        new(
            OptionalInsteadOfNull,
            "add-feature",
            nameof(NullabilityTests<>.Domain_and_feature_classes_return_optional_instead_of_null),
            nameof(NullabilityTests<>.Messages_and_requests_carry_optional_instead_of_null)),
        new(
            InjectedTime,
            "add-feature",
            nameof(DomainRulesTests<>.Domain_never_reads_the_clock_or_creates_random_ids)),
        new(
            NeighboursThroughContracts,
            "add-integration-event",
            nameof(LayerDependencyTests<>.Module_reaches_other_modules_only_through_their_contracts),
            nameof(ModuleGraphTests<>.Modules_do_not_reference_each_other_in_a_cycle)),
        new(
            Layers,
            "add-feature",
            nameof(LayerDependencyTests<>.Domain_depends_only_on_itself_the_domain_building_blocks_and_railway),
            nameof(LayerDependencyTests<>.Features_do_not_depend_on_infrastructure),
            nameof(LayerDependencyTests<>.Endpoints_go_through_the_facade)),
        new(
            Settings,
            "add-settings",
            nameof(LayerDependencyTests<>.Module_does_not_read_configuration_directly)),
        new(
            ModelThroughFactories,
            "add-module",
            nameof(DomainRulesTests<>.Aggregates_entities_and_value_objects_are_created_through_their_factories),
            nameof(DomainRulesTests<>.Domain_types_have_no_public_setters),
            nameof(DomainRulesTests<>.Aggregates_reference_other_aggregates_only_by_id)),
        new(
            QueriesOnlyRead,
            "add-feature",
            nameof(HandlerBoundaryTests<>.Query_handlers_take_no_write_side_dependencies),
            nameof(SqlTextTests<>.Query_handlers_only_read)),
        new(
            ModuleSql,
            "add-feature",
            nameof(SqlTextTests<>.Module_sql_names_its_columns),
            nameof(SqlTextTests<>.Module_sql_names_no_other_module_schema),
            nameof(LayerDependencyTests<>.Module_does_not_open_its_own_database_connections)),
        new(
            Visibility,
            "add-feature",
            nameof(VisibilityTests<>.Handlers_are_internal_and_sealed),
            nameof(VisibilityTests<>.Endpoints_are_internal_and_sealed)),
        new(
            Visibility,
            "add-module",
            nameof(VisibilityTests<>.Module_assembly_exposes_only_its_module_class)),
        new(
            StableNames,
            "add-integration-event",
            nameof(IntegrationEventNameTests<>.Every_integration_event_has_a_unique_stable_name)),
        new(
            StableNames,
            "add-feature",
            nameof(InternalCommandNameTests<>.Every_internal_command_has_a_stable_name_unique_in_its_module)),
        new(
            BuildSettingsInTheCore,
            "",
            nameof(CoreTests<>.Only_the_core_has_build_and_analyzer_config_files)),
    ];

    public static String Explain(String test, String fix) =>
        All
            .Where(line => line.Covers(test))
            .Select(line => line.Explain(fix))
            .DefaultIfEmpty($"Fix: {fix}")
            .First();
}
