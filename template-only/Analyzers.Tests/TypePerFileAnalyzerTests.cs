namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class TypePerFileAnalyzerTests
{
    private const String RuleLine =
        "Rule in AGENTS.md: One type per file, named after the type; types of one name that differ only in type "
        + "parameters share a file, and a `file` type may sit beside them.";

    private const String ExpectedMessage =
        "Type Bar does not belong in Foo.cs: one type per file, named after the type, so move it to Bar.cs. "
        + RuleLine;

    private const String SameNameMessage =
        "Type Foo does not belong in Foo.cs: one type per file, named after the type, so move it to a Foo.cs of its "
        + $"own in the folder of namespace Two. {RuleLine}";

    public static TheoryData<String> SecondTypes =>
    [
        "public sealed class Bar;",
        "public sealed record Bar;",
        "public record struct Bar;",
        "public struct Bar;",
        "public interface Bar;",
        "public enum Bar { One }",
        "public delegate void Bar();",
    ];

    public static TheoryData<String> FileTypes =>
    [
        "file sealed class Helper;",
        "file sealed record Helper;",
        "file record struct Helper;",
        "file struct Helper;",
        "file interface IHelper;",
        "file enum Kind { One }",
        "file delegate void Callback();",
    ];

    [Theory]
    [MemberData(nameof(SecondTypes))]
    public async Task Second_type_beside_a_class_is_reported(String second)
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App;",
            "public sealed class Foo;",
            second);

        lines.ShouldBe([3]);
    }

    [Fact]
    public async Task Message_names_the_type_and_the_file()
    {
        var messages = await AnalyzerRunner.GetReportedMessagesAsync(
            new TypePerFileAnalyzer(),
            "class Foo;\nclass Bar;",
            "src/Foo.cs");

        messages.ShouldBe([ExpectedMessage]);
    }

    [Theory]
    [MemberData(nameof(FileTypes))]
    public async Task File_type_beside_the_type_of_the_file_passes(String fileType)
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App;",
            "public sealed class Foo;",
            fileType);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task File_that_holds_only_file_types_passes()
    {
        var lines = await GetReportedLinesAsync(
            "Helpers.cs",
            "namespace App;",
            "file sealed class Helper;",
            "file static class Extensions;");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Message_for_the_same_name_in_another_namespace_names_the_folder_of_that_namespace()
    {
        var messages = await AnalyzerRunner.GetReportedMessagesAsync(
            new TypePerFileAnalyzer(),
            "namespace One { public sealed class Foo; }\nnamespace Two { public sealed class Foo<T>; }",
            "src/Foo.cs");

        messages.ShouldBe([SameNameMessage]);
    }

    [Fact]
    public async Task Types_of_one_name_that_differ_in_type_parameters_share_a_file()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App;",
            "public sealed class Foo;",
            "public sealed class Foo<T>;",
            "public sealed class Foo<T1, T2>;");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generic_first_in_the_file_still_matches_the_file_name()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App;",
            "public interface Foo<T>;",
            "public interface Foo;");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Same_name_in_another_namespace_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace One { public sealed class Foo; }",
            "namespace Two { public sealed class Foo<T>; }");

        lines.ShouldBe([2]);
    }

    [Fact]
    public async Task Nested_types_do_not_count()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App;",
            "public sealed class Foo",
            "{",
            "    public sealed class Inner;",
            "    public enum Kind { One }",
            "}");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task File_named_after_another_type_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            "Bar.cs",
            "namespace App;",
            "public sealed class Foo;");

        lines.ShouldBe([2]);
    }

    [Fact]
    public async Task Part_of_a_partial_type_in_a_dotted_file_name_passes()
    {
        var lines = await GetReportedLinesAsync(
            "src/Foo.Part.cs",
            "namespace App;",
            "public sealed partial class Foo;");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Types_in_block_namespaces_follow_the_same_rules()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "namespace App",
            "{",
            "    namespace Inner",
            "    {",
            "        public sealed class Foo;",
            "        public sealed class Bar;",
            "    }",
            "",
            "    public sealed class Baz;",
            "}");

        lines.ShouldBe([6, 9]);
    }

    [Fact]
    public async Task Types_in_the_global_namespace_follow_the_same_rules()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.cs",
            "public sealed class Foo;",
            "public sealed class Bar;");

        lines.ShouldBe([2]);
    }

    [Fact]
    public async Task Both_types_are_reported_when_none_matches_the_file_name()
    {
        var lines = await GetReportedLinesAsync(
            "Types.cs",
            "public sealed class Foo;",
            "public sealed class Bar;");

        lines.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task File_without_type_declarations_passes()
    {
        var lines = await GetReportedLinesAsync(
            "Program.cs",
            "global using System;",
            "[assembly: System.CLSCompliant(false)]",
            "System.Console.WriteLine();");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generated_code_is_not_reported()
    {
        var lines = await GetReportedLinesAsync(
            "Foo.g.cs",
            "// <auto-generated/>",
            "public sealed class Foo;",
            "public sealed class Bar;");

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String path, params String[] lines) =>
        AnalyzerRunner.GetReportedLinesAsync(new TypePerFileAnalyzer(), String.Join('\n', lines), path);
}
