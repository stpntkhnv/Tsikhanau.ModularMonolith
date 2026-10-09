namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class StatementSpacingAnalyzerTests
{
    [Fact]
    public async Task Statement_right_after_a_multiline_one_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            items
                .Where(item => item > 0)
                .ToList();
            return items;
            """);

        lines.ShouldBe([4]);
    }

    [Fact]
    public async Task Blank_line_after_a_multiline_statement_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            items
                .Where(item => item > 0)
                .ToList();

            return items;
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Single_line_statements_need_no_blank_line()
    {
        var lines = await GetReportedLinesAsync(
            """
            var first = items.First();
            var last = items.Last();
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Statements_inside_a_method_body_are_checked()
    {
        var lines = await GetReportedLinesAsync(
            """
            class Sample
            {
                void Run()
                {
                    if (ready)
                    {
                        Start();
                    }
                    Stop();
                }
            }
            """);

        lines.ShouldBe([9]);
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new StatementSpacingAnalyzer(), source);
}
