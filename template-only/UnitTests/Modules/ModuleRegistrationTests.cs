using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Contracts;
using Tsikhanau.Monolith.TestModules.Pongs.Features.CountPongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;
using Tsikhanau.Monolith.TestModules.Unhandled;
using Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ModuleRegistrationTests
{
    private const String Scenarios = "Tsikhanau.Monolith.UnitTests.Modules.Scenarios";

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
            "Tsikhanau.Monolith.TestModules.Pongs");

        services.ShouldContain(service => service.ServiceType == typeof(IQueryHandler<CountPongsQuery, Int32>));
    }

    [Fact]
    public void Unhandled_query_in_the_assembly_of_the_query_facade_fails_registration()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            Register<IUnhandledModule, IUnhandledQueryFacade, UnhandledModuleFacade>(
                "Tsikhanau.Monolith.TestModules.Unhandled"));

        exception.Message.ShouldContain("Tsikhanau.Monolith.TestModules.Unhandled.UnhandledQuery has 0 handlers");
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

    [Fact]
    public void Rejects_an_integration_event_without_a_name_even_when_nobody_handles_it()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("UnnamedIntegrationEvent"));

        exception.Message.ShouldContain(
            $"{Scenarios}.UnnamedIntegrationEvent.NamelessIntegrationEvent has no IntegrationEventName attribute");
    }

    [Fact]
    public void Rejects_an_internal_command_that_commits_on_failure()
    {
        const String Problem =
            "is an internal command and cannot commit on failure; the queue records attempts and errors itself";

        var exception = Should.Throw<InvalidOperationException>(() => Register("CommittingInternalCommand"));

        exception.Message.ShouldContain(
            $"{Scenarios}.CommittingInternalCommand.RecordRejectionInternalCommand {Problem}");
    }

    [Fact]
    public void Rejects_a_constraint_name_postgres_would_cut()
    {
        var constraintName = new String('x', 64);

        var exception = Should.Throw<InvalidOperationException>(() => RegisterWithConstraint(constraintName));

        exception.Message.ShouldContain($"'{constraintName}' is 64 bytes long, PostgreSQL cuts names to 63 bytes");
    }

    [Fact]
    public void Constraint_name_length_is_counted_in_utf8_bytes()
    {
        const String TwoByteLetter = "\u00e9";
        var fittingName = $"{new String('x', 61)}{TwoByteLetter}";
        var cutName = $"{new String('x', 62)}{TwoByteLetter}";

        var exception = Should.Throw<InvalidOperationException>(() => RegisterWithConstraint(cutName));

        cutName.Length.ShouldBe(63);
        exception.Message.ShouldContain($"'{cutName}' is 64 bytes long");
        Should.NotThrow(() => RegisterWithConstraint(fittingName));
    }

    [Fact]
    public void Rejects_an_internal_command_without_a_name()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("UnnamedInternalCommand"));

        exception.Message.ShouldContain(
            $"{Scenarios}.UnnamedInternalCommand.UnnamedInternalCommand has no InternalCommandName attribute");
    }

    [Fact]
    public void Rejects_an_internal_command_with_a_blank_name()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Register("BlankInternalCommandName"));

        exception.Message.ShouldContain(
            $"{Scenarios}.BlankInternalCommandName.BlankNamedInternalCommand has a blank InternalCommandName");
    }

    [Fact]
    public void Rejects_two_internal_commands_with_one_name()
    {
        var commandTypeNames =
            $"{typeof(RemindInternalCommand).FullName} and {typeof(RemindAgainInternalCommand).FullName}";

        var exception = Should.Throw<InvalidOperationException>(() => Register("SharedInternalCommandName"));

        exception.Message.ShouldContain($"Internal command name 'remind' is used by {commandTypeNames}");
    }

    private static IServiceCollection Register(String scenario) =>
        Register<ITestModule, ITestQueryFacade, TestModuleFacade>($"{Scenarios}.{scenario}");

    private static IServiceCollection Register<TModule, TQueryFacade, TFacade>(String rootNamespace)
        where TModule : class, IModuleFacade, TQueryFacade
        where TQueryFacade : class, IModuleQueryFacade
        where TFacade : class, TModule =>
        new ServiceCollection().AddModuleInfrastructure<TModule, TQueryFacade, TFacade, TestDbContext>(
            new ModuleDefinition("Test", "test", typeof(ModuleRegistrationTests).Assembly, rootNamespace));

    private static IServiceCollection RegisterWithConstraint(String constraintName) =>
        new ServiceCollection().AddModuleInfrastructure<ITestModule, ITestQueryFacade, TestModuleFacade, TestDbContext>(
            new ModuleDefinition("Test", "test", typeof(ModuleRegistrationTests).Assembly, $"{Scenarios}.Valid")
            {
                ConstraintErrors = new Dictionary<String, Error>
                {
                    [constraintName] = Error.Conflict("Tests.Taken", "Already taken"),
                },
            });

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
