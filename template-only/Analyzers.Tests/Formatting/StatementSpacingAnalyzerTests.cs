using Tsikhanau.Monolith.Analyzers.Formatting;
using Tsikhanau.Monolith.Analyzers.Tests.Running;

namespace Tsikhanau.Monolith.Analyzers.Tests.Formatting;

public sealed class StatementSpacingAnalyzerTests
{
    private const String CrowdedStatementMessage =
        "Leave a blank line after the multi-line statement above. Rule in AGENTS.md: Lines fit in 120 characters, "
        + "string literals included; only the text inside a multi-line raw string literal is data and keeps its "
        + "lines. A chain of two calls or more puts each call on its own line, and a statement that spans several "
        + "lines is followed by a blank line unless the block ends there.";

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

    [Fact]
    public async Task Diagnostic_marks_the_first_token_of_the_crowded_statement_in_globals_blocks_and_switch_sections()
    {
        var diagnostics = await AnalyzerRunner.GetReportedDiagnosticsAsync(
            new StatementSpacingAnalyzer(),
            """
            Start(
                1);
            Stop();

            class Sample
            {
                void Run(Int32 value)
                {
                    if (value > 0)
                    {
                        Start(
                            value);
                        Stop();
                    }

                    switch (value)
                    {
                        case 1:
                            Start(
                                value);
                            Stop();
                            break;
                    }
                }
            }
            """);

        diagnostics.ShouldBe(
        [
            $"TS0002 3:1-3:5 {CrowdedStatementMessage}",
            $"TS0002 13:13-13:17 {CrowdedStatementMessage}",
            $"TS0002 21:17-21:21 {CrowdedStatementMessage}",
        ]);
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new StatementSpacingAnalyzer(), source);
}
