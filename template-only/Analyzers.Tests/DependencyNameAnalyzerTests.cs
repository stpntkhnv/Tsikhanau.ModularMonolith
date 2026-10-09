namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class DependencyNameAnalyzerTests
{
    private const String Types = """
        interface IPingRepository { }
        class TimeProvider { }
        class EchoStarter { }
        interface IOptions<T> { }
        class Settings { }
        interface Identifiable { }
        sealed record ModuleDescriptor(string Name);
        class WebApplication { }
        interface IApplicationBuilder { }
        interface IEndpointRouteBuilder { }
        interface IServiceCollection { }
        """;

    [Fact]
    public async Task Dependency_named_after_its_type_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(
                IPingRepository pingRepository,
                TimeProvider timeProvider,
                Identifiable identifiable)
            { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dependency_named_after_what_it_holds_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(
                IPingRepository pings,
                TimeProvider time)
            { }
            """);

        lines.ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Qualifier_in_front_of_the_type_name_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(TimeProvider fakeTimeProvider, EchoStarter adminEchoStarter) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Name_that_is_not_camel_case_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(
                TimeProvider AdminTimeProvider,
                TimeProvider _TimeProvider)
            { }
            """);

        lines.ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Generic_arguments_are_not_part_of_the_name()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(
                IOptions<Settings> options,
                IOptions<Settings> settings)
            { }
            """);

        lines.ShouldBe([3]);
    }

    [Fact]
    public async Task Framework_names_pass()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Host(WebApplication app) { }

            sealed class Pipeline(IApplicationBuilder app) { }

            sealed class Routes(IEndpointRouteBuilder endpoints) { }

            sealed class Registration(IServiceCollection services) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Framework_name_of_another_framework_type_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Host(
                WebApplication services,
                IServiceCollection app)
            { }
            """);

        lines.ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Public_ordinary_constructor_is_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Verifier
            {
                public Verifier(TimeProvider time) { }
            }
            """);

        lines.ShouldBe([3]);
    }

    [Fact]
    public async Task Private_constructor_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Aggregate
            {
                private Aggregate(EchoStarter starter) { }
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task String_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(string connectionString) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Value_type_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(int count) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_type_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Handler(ModuleDescriptor descriptor) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_constructor_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed record Message(EchoStarter starter);
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Type_parameter_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            abstract class Entity<TId>(TId id) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Struct_constructor_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            struct Pair
            {
                public Pair(EchoStarter starter) { }
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generic_collection_is_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Batch(System.Collections.Generic.IReadOnlyList<EchoStarter> items) { }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Non_generic_collections_are_not_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            sealed class Batch(
                System.Collections.IEnumerable items,
                System.Collections.IList values,
                System.Collections.IDictionary pairs)
            { }
            """);

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new DependencyNameAnalyzer(), source + Environment.NewLine + Types);
}
