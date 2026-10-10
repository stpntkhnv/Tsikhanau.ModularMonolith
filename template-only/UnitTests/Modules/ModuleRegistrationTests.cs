using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
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
using Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Misplaced.Infrastructure;
using Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Nested;
using Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Queryless;
using Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ModuleRegistrationTests
{
    private const String Scenarios = "Tsikhanau.Monolith.UnitTests.Modules.Scenarios";

    private const String NameProblem = "must be named I<Module>Module, like INotesModule";

    private const String RootProblem = "must sit at the root of its module, in a namespace ending with";

    private const String SchemaProblem = "must name its schema in a const String SchemaName";

    private const String QueryFacadeProblem = "must extend at most one query facade, like INotesQueryFacade";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

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
        var services = new ServiceCollection()
            .AddModuleInfrastructure<IPongsModule, PongsModuleFacade, TestDbContext>();

        services.ShouldContain(service => service.ServiceType == typeof(IQueryHandler<CountPongsQuery, Int32>));
    }

    [Fact]
    public void Name_schema_and_query_facade_come_from_the_facade_and_the_db_context()
    {
        var services = new ServiceCollection()
            .AddModuleInfrastructure<IPongsModule, PongsModuleFacade, TestDbContext>();

        var module = services
            .GetOrAddInstance<ModuleRegistry>().Modules
            .ShouldHaveSingleItem();

        module.Key.ShouldBe(typeof(IPongsModule));
        module.Name.ShouldBe("Pongs");
        module.Schema.ShouldBe("test");
        module.QueryFacadeKey.ShouldBe(Optional<Type>.Some(typeof(IPongsQueryFacade)));
        module.DbContextType.ShouldBe(typeof(TestDbContext));
        module.ConstraintErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Constraint_errors_reach_the_module_descriptor()
    {
        var constraintErrors = new Dictionary<String, Error> { ["pongs_label_key"] = PongTaken };

        var services = new ServiceCollection()
            .AddModuleInfrastructure<IPongsModule, PongsModuleFacade, TestDbContext>(constraintErrors);

        var module = services
            .GetOrAddInstance<ModuleRegistry>().Modules
            .ShouldHaveSingleItem();

        module.ConstraintErrors.ShouldBe(constraintErrors);
    }

    [Fact]
    public void Rejects_a_facade_named_against_the_convention()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddModuleInfrastructure<ITestFacade, TestFacade, TestDbContext>());

        exception.Message.ShouldBe($"Module facade {typeof(ITestFacade).FullName} {NameProblem}");
    }

    [Fact]
    public void Rejects_a_facade_without_the_interface_prefix_even_when_its_module_name_starts_with_i()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddModuleInfrastructure<InventoryModule, InventoryModuleFacade, TestDbContext>());

        exception.Message.ShouldBe($"Module facade {typeof(InventoryModule).FullName} {NameProblem}");
    }

    [Fact]
    public void Rejects_a_facade_nested_in_a_type_even_in_a_namespace_ending_with_its_module_name()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection()
                .AddModuleInfrastructure<NestedFacadeHolder.INestedModule, NestedModuleFacade, TestDbContext>());

        exception.Message.ShouldBe(
            $"Module facade {typeof(NestedFacadeHolder.INestedModule).FullName} {RootProblem} .Nested");
    }

    [Fact]
    public void Rejects_a_facade_in_a_folder_below_the_root_of_its_module()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddModuleInfrastructure<IMisplacedModule, MisplacedModuleFacade, TestDbContext>());

        exception.Message.ShouldBe($"Module facade {typeof(IMisplacedModule).FullName} {RootProblem} .Misplaced");
    }

    [Fact]
    public void Rejects_a_db_context_without_a_schema_name_constant()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddModuleInfrastructure<IPongsModule, PongsModuleFacade, SchemalessDbContext>());

        exception.Message.ShouldBe($"Module db context {typeof(SchemalessDbContext).FullName} {SchemaProblem}");
    }

    [Fact]
    public void Registers_a_module_without_a_query_facade()
    {
        var services = Register<IQuerylessModule, QuerylessModuleFacade>($"{Scenarios}.Queryless");

        var module = services
            .GetOrAddInstance<ModuleRegistry>().Modules
            .ShouldHaveSingleItem();

        module.QueryFacadeKey.ShouldBe(Optional<Type>.None());
    }

    [Fact]
    public async Task Facade_of_a_module_without_a_query_facade_answers_commands()
    {
        var answer = Error.Failure("Queryless.Answer", "Answered without a query facade");
        await using var serviceProvider = Register<IQuerylessModule, QuerylessModuleFacade>($"{Scenarios}.Queryless")
            .AddLogging()
            .BuildServiceProvider();

        var result = await serviceProvider
            .GetRequiredService<IQuerylessModule>()
            .ExecuteCommandAsync(new FailCommand(answer), CancellationToken);

        result.ShouldBe(Result.Failure(answer));
    }

    [Fact]
    public void Rejects_a_facade_with_two_query_facades()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            Register<ITwoQueryFacadesModule, TwoQueryFacadesModuleFacade>($"{Scenarios}.Valid"));

        exception.Message.ShouldBe($"Module facade {typeof(ITwoQueryFacadesModule).FullName} {QueryFacadeProblem}");
    }

    [Fact]
    public void Unhandled_query_in_the_assembly_of_the_query_facade_fails_registration()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            Register<IUnhandledModule, UnhandledModuleFacade>("Tsikhanau.Monolith.TestModules.Unhandled"));

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

    private static Error PongTaken => Error.Conflict("Pongs.Taken", "Pong taken");

    private static IServiceCollection Register(String scenario) =>
        Register<ITestModule, TestModuleFacade>($"{Scenarios}.{scenario}");

    private static IServiceCollection Register<TModule, TFacade>(String rootNamespace)
        where TModule : class, IModuleFacade
        where TFacade : class, TModule =>
        new ServiceCollection().AddModuleInfrastructure<TModule, TFacade, TestDbContext>(
            new ModuleDefinition("Test", "test", typeof(ModuleRegistrationTests).Assembly, rootNamespace));

    private static IServiceCollection RegisterWithConstraint(String constraintName) =>
        new ServiceCollection().AddModuleInfrastructure<ITestModule, TestModuleFacade, TestDbContext>(
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

    private interface ITestFacade : ITestQueryFacade, IModuleFacade;

    private sealed class TestFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<ITestFacade>(serviceScopeFactory), ITestFacade;

    private interface InventoryModule : ITestQueryFacade, IModuleFacade;

    private sealed class InventoryModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<InventoryModule>(serviceScopeFactory), InventoryModule;

    private sealed class NestedModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<NestedFacadeHolder.INestedModule>(serviceScopeFactory), NestedFacadeHolder.INestedModule;

    private sealed class MisplacedModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<IMisplacedModule>(serviceScopeFactory), IMisplacedModule;

    private interface IQuerylessModule : IModuleFacade
    {
        Task<Result<TResult>> ExecuteCommandAsync<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellationToken)
            where TResult : notnull;
    }

    private sealed class QuerylessModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<IQuerylessModule>(serviceScopeFactory), IQuerylessModule
    {
        public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellationToken)
            where TResult : notnull =>
            SendAsync(command, cancellationToken);
    }

    private interface IOtherQueryFacade : IModuleQueryFacade;

    private interface ITwoQueryFacadesModule : ITestQueryFacade, IOtherQueryFacade, IModuleFacade;

    private sealed class TwoQueryFacadesModuleFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<ITwoQueryFacadesModule>(serviceScopeFactory), ITwoQueryFacadesModule;

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
        public const String SchemaName = "test";
    }

    private sealed class SchemalessDbContext(DbContextOptions<SchemalessDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions);
}
