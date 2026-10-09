namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class CallChainAnalyzerTests
{
    [Fact]
    public async Task Chain_on_one_line_is_reported_for_every_call()
    {
        var lines = await GetReportedLinesAsync(
            """
            items.Where(item => item > 0).ToList();
            """);

        lines.ShouldBe([1, 1]);
    }

    [Fact]
    public async Task Chain_with_every_call_on_its_own_line_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            items
                .Where(item => item > 0)
                .ToList();
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task First_call_left_next_to_the_receiver_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            items.Where(item => item > 0)
                .ToList();
            """);

        lines.ShouldBe([1]);
    }

    [Fact]
    public async Task Single_call_passes()
    {
        var lines = await GetReportedLinesAsync("services.AddScoped(service, implementation);");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Properties_before_the_first_call_stay_on_the_receiver_line()
    {
        var lines = await GetReportedLinesAsync(
            """
            app.ApplicationServices
                .GetServices()
                .ForEach(module => module.UsePipeline(app));
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Await_starts_a_new_chain()
    {
        var lines = await GetReportedLinesAsync("(await client.GetAsync()).ShouldBe(expected);");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Calls_after_await_still_form_a_chain()
    {
        var lines = await GetReportedLinesAsync("(await rows.LoadAsync()).Single().Processed.ShouldBeTrue();");

        lines.ShouldBe([1, 1]);
    }

    [Fact]
    public async Task Call_on_the_result_of_a_call_without_a_receiver_is_reported()
    {
        var lines = await GetReportedLinesAsync("Double(2).ShouldBe(4);");

        lines.ShouldBe([1]);
    }

    [Fact]
    public async Task Call_after_a_call_without_a_receiver_passes_on_its_own_line()
    {
        var lines = await GetReportedLinesAsync(
            """
            Double(2)
                .ShouldBe(4);
            """);

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String statements) =>
        AnalyzerRunner.GetReportedLinesAsync(new CallChainAnalyzer(), statements);
}
