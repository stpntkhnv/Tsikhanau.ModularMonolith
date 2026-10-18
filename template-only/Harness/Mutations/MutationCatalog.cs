using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class MutationCatalog
{
    private readonly Dictionary<String, Func<Result<Unit>>> _mutations;

    public MutationCatalog(SourceTree sourceTree)
    {
        var endpoints = new EndpointMutations(sourceTree);
        var handlerDependencies = new HandlerDependencyMutations(sourceTree);
        var handlerResults = new HandlerResultMutations(sourceTree);
        var moduleBoundaries = new ModuleBoundaryMutations(sourceTree);
        var aggregates = new AggregateMutations(sourceTree);
        var domainDependencies = new DomainDependencyMutations(sourceTree);
        var messages = new MessageMutations(sourceTree);
        var sql = new SqlMutations(sourceTree);
        var persistence = new PersistenceMutations(sourceTree);
        var eventHandlers = new EventHandlerMutations(sourceTree);

        _mutations = new(StringComparer.Ordinal)
        {
            ["m01_endpoint_repository"] = endpoints.EndpointUsesRepository,
            ["m02_endpoint_dbcontext"] = endpoints.EndpointUsesDbContext,
            ["m03_endpoint_raw_sql"] = endpoints.EndpointRunsRawSql,
            ["m32_logic_in_endpoint"] = endpoints.EndpointHoldsLogic,
            ["m27_handler_uses_dbcontext"] = handlerDependencies.HandlerUsesDbContext,
            ["x1_feature_handler_takes_ef_dbcontext_base"] = handlerDependencies.HandlerTakesEfDbContextBase,
            ["x2_feature_handler_takes_npgsql_data_source"] = handlerDependencies.HandlerTakesNpgsqlDataSource,
            ["x3_feature_handler_takes_http_context"] = handlerDependencies.HandlerTakesHttpContext,
            ["m28r_handler_reads_configuration"] = handlerDependencies.HandlerReadsConfiguration,
            ["m26_handler_not_sealed"] = handlerDependencies.HandlerNotSealed,
            ["m04_module_references_other_module_project"] = moduleBoundaries.ModuleReferencesOtherModuleProject,
            ["m05_testmodule_uses_other_module_domain"] = moduleBoundaries.TestModuleUsesOtherModuleDomain,
            ["m16_handler_for_foreign_query"] = moduleBoundaries.HandlerForForeignQuery,
            ["m29_module_not_added_to_host"] = moduleBoundaries.ModuleNotAddedToHost,
            ["m06_public_setter_on_aggregate"] = aggregates.PublicSetterOnAggregate,
            ["m07r_public_field_and_static_counter_on_aggregate"] = aggregates.PublicFieldAndStaticCounterOnAggregate,
            ["m35_domain_event_mutable_and_public_ctor_aggregate"] = aggregates.PublicConstructorOnAggregate,
            ["m37_events_raised_outside_aggregate"] = aggregates.StateChangedWithoutEvent,
            ["m08_domain_uses_ef_attribute"] = domainDependencies.DomainUsesEfAttribute,
            ["m08d_domain_method_takes_dbcontext"] = domainDependencies.DomainMethodTakesDbContext,
            ["x5_testmodule_domain_method_takes_dbcontext"] = domainDependencies.TestModuleDomainMethodTakesDbContext,
            ["x4_domain_uses_npgsql"] = domainDependencies.DomainUsesNpgsql,
            ["m08e_domain_uses_application_layer"] = domainDependencies.DomainUsesApplicationLayer,
            ["m08c2_domain_attribute_points_to_ef_and_infrastructure"] =
                domainDependencies.DomainAttributePointsToEfAndInfrastructure,
            ["m31_mutable_command"] = messages.MutableCommand,
            ["m09_handler_throws_for_expected_failure"] = handlerResults.HandlerThrowsForExpectedFailure,
            ["m10_repository_returns_null"] = persistence.RepositoryReturnsNull,
            ["m10b_query_result_nullable"] = messages.QueryResultNullable,
            ["m18_result_ignored"] = handlerResults.ResultIgnored,
            ["m18b_result_discarded_explicitly"] = handlerResults.ResultDiscardedExplicitly,
            ["m18c_task_result_not_awaited"] = handlerResults.TaskResultNotAwaited,
            ["m19_value_without_check"] = handlerResults.ValueWithoutCheck,
            ["m36_result_value_after_wrong_check"] = handlerResults.ValueAfterWrongCheck,
            ["m11_clock_in_handler"] = handlerDependencies.HandlerReadsTheClock,
            ["m12_clock_in_domain"] = aggregates.DomainReadsTheClock,
            ["m12b_timeprovider_system_in_domain"] = aggregates.DomainUsesSystemTimeProvider,
            ["m55_domain_reads_timeprovider"] = aggregates.DomainTakesTimeProvider,
            ["m12c_newguid_in_handler"] = handlerDependencies.HandlerCreatesRandomId,
            ["m13_integration_event_without_name"] = messages.IntegrationEventWithoutName,
            ["m14_integration_event_carries_domain_type"] = messages.IntegrationEventCarriesDomainType,
            ["m14c_integration_event_carries_buildingblock_domain_type"] =
                messages.IntegrationEventCarriesBuildingBlockDomainType,
            ["m15_query_handler_writes"] = sql.QueryHandlerWrites,
            ["m24_cross_schema_sql"] = sql.SqlNamesOtherModuleSchema,
            ["m24b_cross_schema_sql_in_other_module"] = sql.OtherModuleSqlNamesNotesSchema,
            ["m33_sql_injection_dapper"] = sql.SqlBuiltFromInput,
            ["m25_migration_in_wrong_schema"] = persistence.MigrationInWrongSchema,
            ["m25b_migration_in_public_schema"] = persistence.MigrationInPublicSchema,
            ["m17_int_and_string_keywords"] = aggregates.KeywordTypeNames,
            ["m20_static_mutable_state"] = handlerResults.StaticMutableState,
            ["m21_async_void"] = handlerResults.AsyncVoid,
            ["m22_task_result_blocking"] = eventHandlers.TaskResultBlocks,
            ["m18d_task_returned_not_awaited_sync_method"] = handlerResults.TaskWaitedSynchronously,
            ["m23_cancellation_token_not_passed"] = persistence.CancellationTokenNotPassed,
            ["m56_pragma_hides_analyzer"] = persistence.PragmaHidesAnalyzer,
            ["m30_exception_swallowed"] = eventHandlers.ExceptionSwallowed,
        };
    }

    public Result<Unit> Apply(String name) =>
        _mutations.TryGetValue(name, out var mutation)
            ? mutation()
            : Error.NotFound("harness.mutation", $"no mutation named {name}");
}
