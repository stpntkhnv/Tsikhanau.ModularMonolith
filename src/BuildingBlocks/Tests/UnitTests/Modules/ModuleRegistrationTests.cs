using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Contracts.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs;
using ModularMonolith.TestModules.Pongs.Contracts;
using ModularMonolith.TestModules.Pongs.Features.CountPongs;
using ModularMonolith.TestModules.Pongs.Infrastructure;
using ModularMonolith.TestModules.Unhandled;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules;

public sealed class ModuleRegistrationTests
{
    private const String Scenarios = "ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios";

    [Fact]
    public void Registers_a_module_with_one_handler_per_request() =>
        Should.NotThrow(() => Register("Valid"));

    [Fact]
    public void Rejects_a_request_without_handler()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("Unhandled"));

        exception.Message.ShouldContain("UnhandledCommand has 0 handlers");
    }

    [Fact]
    public void Rejects_a_request_with_two_handlers()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("TwiceHandled"));

        exception.Message.ShouldContain("TwiceHandledCommand has 2 handlers");
    }

    [Fact]
    public void Query_facade_is_the_module_facade()
    {
        var services = Register("Valid");
        using var serviceProvider = services.BuildServiceProvider();

        var queryFacade = serviceProvider.GetRequiredService<ITestQueryFacade>();
        var moduleFacade = serviceProvider.GetRequiredService<ITestModule>();

        queryFacade.ShouldBeSameAs(moduleFacade);
    }

    [Fact]
    public void Handlers_are_found_in_the_assembly_of_the_query_facade()
    {
        var services = Register<IPongsModule, IPongsQueryFacade, PongsModuleFacade>(
            "ModularMonolith.TestModules.Pongs");

        services.ShouldContain(service => service.ServiceType == typeof(IQueryHandler<CountPongsQuery, Int32>));
    }

    [Fact]
    public void Unhandled_query_in_the_assembly_of_the_query_facade_fails_registration()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            Register<IUnhandledModule, IUnhandledQueryFacade, UnhandledModuleFacade>(
                "ModularMonolith.TestModules.Unhandled"));

        exception.Message.ShouldContain("ModularMonolith.TestModules.Unhandled.UnhandledQuery has 0 handlers");
    }

    [Fact]
    public void Module_rejects_a_handler_of_a_foreign_request()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("Owner"));

        exception.Message.ShouldContain(
            $"{Scenarios}.Owner.ForeignQueryHandler handles the foreign request {Scenarios}.Foreign.ForeignQuery");
    }

    [Fact]
    public void Rejects_an_event_handled_both_inside_and_outside_a_transaction()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("MixedTransaction"));

        exception.Message.ShouldContain(
            "MixedHappenedIntegrationEvent has handlers both inside and outside a transaction");
    }

    [Fact]
    public void Rejects_the_marker_on_something_that_is_not_an_integration_event_handler()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("StrayMarker"));

        exception.Message.ShouldContain("StrayMarker runs outside a transaction but handles no integration event");
    }

    private static IServiceCollection Register(String scenario) =>
        Register<ITestModule, ITestQueryFacade, TestModuleFacade>($"{Scenarios}.{scenario}");

    private static IServiceCollection Register<TModule, TQueryFacade, TFacade>(String rootNamespace)
        where TModule : class, IModuleFacade, TQueryFacade
        where TQueryFacade : class, IModuleQueryFacade
        where TFacade : class, TModule =>
        new ServiceCollection().AddModuleInfrastructure<TModule, TQueryFacade, TFacade, TestDbContext>(
            new ModuleDefinition("Test", "test", typeof(ModuleRegistrationTests).Assembly, rootNamespace));

    private interface ITestQueryFacade : IModuleQueryFacade;

    private interface ITestModule : ITestQueryFacade, IModuleFacade;

    private sealed class TestModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<ITestModule>(serviceScopeFactory), ITestModule;

    private interface IUnhandledModule : IUnhandledQueryFacade, IModuleFacade;

    private sealed class UnhandledModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<IUnhandledModule>(serviceScopeFactory), IUnhandledModule
    {
        public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
            IUnhandledQuery<TResult> query,
            CancellationToken cancellationToken)
            where TResult : notnull =>
            QueryAsync(query, cancellationToken);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        protected override String Schema => "test";
    }
}
