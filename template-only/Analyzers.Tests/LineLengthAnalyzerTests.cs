namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class LineLengthAnalyzerTests
{
    private const String LineOf123CharactersMessage =
        "Line is 123 characters long, more than 120: wrap it at the highest level first (STYLE.md 9), name a long "
        + "string or split it into parts joined by `+`. Rule in AGENTS.md: Lines fit in 120 characters, string "
        + "literals included; only the text inside a multi-line raw string literal is data and keeps its lines. A "
        + "chain of two calls or more puts each call on its own line, and a statement that spans several lines is "
        + "followed by a blank line unless the block ends there.";

    public static TheoryData<String, String> OneLineLiterals =>
        new()
        {
            { "var text = \"", "\";" },
            { "var text = $\"{name} ", "\";" },
            { "var text = @\"", "\";" },
            { "var text = $@\"{name} ", "\";" },
            { "var text = \"\"\"", "\"\"\";" },
            { "var text = $\"\"\"{name} ", "\"\"\";" },
            { "var bytes = \"", "\"u8;" },
        };

    public static TheoryData<String, String> MultilineRawLiterals =>
        new()
        {
            { "var text = \"\"\"", "    \"\"\";" },
            { "var text = \"\"\"\"", "    \"\"\"\";" },
            { "var bytes = \"\"\"", "    \"\"\"u8;" },
            { "var bytes = \"\"\"\"", "    \"\"\"\"u8;" },
            { "var text = $\"\"\"", "    \"\"\";" },
            { "var text = $$\"\"\"", "    \"\"\";" },
        };

    public static TheoryData<String> MultilineRawOpenings =>
    [
        "\"\"\"",
        "\"\"\"\"",
        "$\"\"\"",
        "$$\"\"\"",
    ];

    [Fact]
    public async Task Line_of_121_characters_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            Source(
                "var first = 0;",
                Line("var ", 121, " = 0;")));

        lines.ShouldBe([2]);
    }

    [Fact]
    public async Task Line_of_120_characters_passes()
    {
        var lines = await GetReportedLinesAsync(Line("var ", 120, " = 0;"));

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Diagnostic_states_the_length_the_fix_and_the_rule_line()
    {
        var messages = await AnalyzerRunner.GetReportedMessagesAsync(
            new LineLengthAnalyzer(),
            Line("var ", 123, " = 0;"));

        messages.ShouldBe([LineOf123CharactersMessage]);
    }

    [Fact]
    public async Task Long_comment_line_is_reported()
    {
        var lines = await GetReportedLinesAsync(Line("// ", 121, ""));

        lines.ShouldBe([1]);
    }

    [Theory]
    [MemberData(nameof(OneLineLiterals))]
    public async Task Line_long_because_of_a_one_line_literal_is_reported(String start, String end)
    {
        var lines = await GetReportedLinesAsync(Line(start, 121, end));

        lines.ShouldBe([1]);
    }

    [Theory]
    [MemberData(nameof(MultilineRawLiterals))]
    public async Task Long_text_lines_inside_a_multiline_raw_literal_pass(String opening, String closing)
    {
        var lines = await GetReportedLinesAsync(
            Source(
                opening,
                Line("    ", 121, ""),
                "    short",
                Line("    ", 200, ""),
                closing));

        lines.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(MultilineRawOpenings))]
    public async Task Long_opening_line_of_a_multiline_raw_literal_is_reported(String opening)
    {
        var lines = await GetReportedLinesAsync(
            Source(
                Line("var ", 121, $" = {opening}"),
                "    text",
                $"    {opening.TrimStart('$')};"));

        lines.ShouldBe([1]);
    }

    [Theory]
    [MemberData(nameof(MultilineRawOpenings))]
    public async Task Long_closing_line_of_a_multiline_raw_literal_is_reported(String opening)
    {
        var lines = await GetReportedLinesAsync(
            Source(
                $"var text = {opening}",
                "    text",
                Line($"    {opening.TrimStart('$')} + ", 121, ";")));

        lines.ShouldBe([3]);
    }

    [Fact]
    public async Task Quote_lines_long_only_through_whitespace_are_reported()
    {
        var indentation = new String(' ', 118);
        var lines = await GetReportedLinesAsync(
            Source(
                "\"\"\"" + indentation,
                indentation + "text",
                indentation + "\"\"\""));

        lines.ShouldBe([1, 3]);
    }

    [Fact]
    public async Task Long_line_holding_an_interpolation_hole_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            Source(
                "var text = $\"\"\"",
                Line("    {name} ", 121, ""),
                Line("    ", 121, " {name}"),
                Line("    ", 121, ""),
                "    \"\"\";"));

        lines.ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Lines_ending_in_crlf_count_the_same()
    {
        var lines = await GetReportedLinesAsync(
            String.Join(
                "\r\n",
                Line("var ", 121, " = 0;"),
                Line("var ", 120, " = 0;"),
                "var text = \"\"\"",
                Line("    ", 121, ""),
                "    \"\"\";",
                "var interpolated = $\"\"\"",
                Line("    {name} ", 121, ""),
                Line("    ", 121, ""),
                "    \"\"\";"));

        lines.ShouldBe([1, 7]);
    }

    [Fact]
    public async Task Generated_file_passes()
    {
        var lines = await GetReportedLinesAsync(
            Source(
                "// <auto-generated/>",
                Line("var ", 121, " = 0;")));

        lines.ShouldBeEmpty();
    }

    private static String Source(params String[] lines) => String.Join('\n', lines);

    private static String Line(String start, Int32 length, String end) =>
        start + new String('a', length - start.Length - end.Length) + end;

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new LineLengthAnalyzer(), source);
}
