namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class ClrTypeNameAnalyzerTests
{
    [Fact]
    public async Task Keyword_type_of_a_local_is_reported()
    {
        var lines = await GetReportedLinesAsync("int count = 0;");

        lines.ShouldBe([1]);
    }

    [Fact]
    public async Task Keyword_before_a_member_access_is_reported()
    {
        var lines = await GetReportedLinesAsync("var text = string.Empty;");

        lines.ShouldBe([1]);
    }

    [Fact]
    public async Task Keywords_inside_nullable_typeof_and_casts_are_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            long? total = null;
            var type = typeof(object);
            var rounded = (short)1;
            """);

        lines.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Clr_type_names_and_void_pass()
    {
        var lines = await GetReportedLinesAsync(
            """
            using System;

            class Sample
            {
                void Run(Int32 count, String text, Boolean isReady) { }
            }
            """);

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new ClrTypeNameAnalyzer(), source);
}
