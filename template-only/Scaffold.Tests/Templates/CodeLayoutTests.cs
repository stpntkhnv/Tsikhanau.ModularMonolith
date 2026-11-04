using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Templates;

public sealed class CodeLayoutTests
{
    private const String Body = "internal sealed record Sample;";

    private static readonly String LineOf120Characters = new('x', 120);

    private static readonly String LineOf121Characters = new('x', 121);

    [Fact]
    public void Usings_put_system_first_and_the_rest_in_order_ignoring_case()
    {
        var codeFile = CodeLayout.CodeFile(
            "Samples",
            [
                "Zeta.Modules",
                "System.Net",
                "Tsikhanau.Railway",
                "Microsoft.Extensions",
                "System",
                "trip_own.Modules.Notes",
                "Acme.Shop",
            ],
            Body);

        codeFile.ShouldBe(
            """
            using System;
            using System.Net;
            using Acme.Shop;
            using Microsoft.Extensions;
            using trip_own.Modules.Notes;
            using Tsikhanau.Railway;
            using Zeta.Modules;

            namespace Samples;

            internal sealed record Sample;

            """);
    }

    [Fact]
    public void Usings_name_each_namespace_once()
    {
        var codeFile = CodeLayout.CodeFile(
            "Samples",
            ["Tsikhanau.Railway", "Acme.Shop", "System", "Tsikhanau.Railway", "System"],
            Body);

        codeFile.ShouldBe(
            """
            using System;
            using Acme.Shop;
            using Tsikhanau.Railway;

            namespace Samples;

            internal sealed record Sample;

            """);
    }

    [Fact]
    public void Code_file_keeps_a_using_of_a_namespace_that_encloses_its_own()
    {
        var codeFile = CodeLayout.CodeFile("Tsikhanau.Railway.Modules.Notes", ["Tsikhanau.Railway"], Body);

        codeFile.ShouldBe(
            """
            using Tsikhanau.Railway;

            namespace Tsikhanau.Railway.Modules.Notes;

            internal sealed record Sample;

            """);
    }

    [Fact]
    public void First_form_whose_lines_all_fit_is_taken()
    {
        var code = CodeLayout.FirstThatFits($"short\n{LineOf121Characters}", $"short\n{LineOf120Characters}", "last");

        code.ShouldBe($"short\n{LineOf120Characters}");
    }

    [Fact]
    public void Last_form_is_taken_when_none_fits()
    {
        var code = CodeLayout.FirstThatFits(LineOf121Characters, $"{LineOf121Characters}y");

        code.ShouldBe($"{LineOf121Characters}y");
    }

    [Fact]
    public void Too_long_line_of_a_csharp_file_is_found_with_its_place()
    {
        IReadOnlyList<NewFile> newFiles =
        [
            new("a/Long.csproj", LineOf121Characters),
            new("a/Fits.cs", $"{LineOf120Characters}\n"),
            new("a/Long.cs", $"first\n{LineOf121Characters}\n"),
        ];

        var tooLongLine = CodeLayout.FindTooLongLine(newFiles);

        tooLongLine.ShouldBe(Optional<String>.Some("a/Long.cs:2 121 characters long"));
    }

    [Fact]
    public void Files_whose_lines_all_fit_have_no_too_long_line()
    {
        IReadOnlyList<NewFile> newFiles = [new("a/Fits.cs", $"{LineOf120Characters}\n")];

        var tooLongLine = CodeLayout.FindTooLongLine(newFiles);

        tooLongLine.ShouldBe(Optional<String>.None());
    }
}
