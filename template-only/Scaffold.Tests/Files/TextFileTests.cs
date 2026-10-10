using System.Text;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Files;

public sealed class TextFileTests
{
    private static readonly Byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void Planned_edit_keeps_the_original_bytes_next_to_the_edited_ones()
    {
        var file = new TextFile("Program.cs", Encoding.UTF8.GetBytes("first\n"));

        var plannedEdit = file.PlanEdit(text => text + "second\n").Value;

        plannedEdit.Path.ShouldBe("Program.cs");
        plannedEdit.Original.ShouldBe(Encoding.UTF8.GetBytes("first\n"));
        plannedEdit.Updated.ShouldBe(Encoding.UTF8.GetBytes("first\nsecond\n"));
    }

    [Fact]
    public void Planned_edit_keeps_the_byte_order_mark_of_the_file()
    {
        Byte[] content = [.. ByteOrderMark, .. Encoding.UTF8.GetBytes("{}\n")];
        var file = new TextFile("appsettings.json", content);

        var plannedEdit = file.PlanEdit(text => text.Replace("{}", "{ }", StringComparison.Ordinal));

        file.Text.ShouldBe("{}\n");
        plannedEdit.Value.Updated.ShouldBe([.. ByteOrderMark, .. Encoding.UTF8.GetBytes("{ }\n")]);
    }

    [Fact]
    public void Text_is_decoded_once()
    {
        var file = new TextFile("Program.cs", Encoding.UTF8.GetBytes("var app = 1;\n"));

        var firstRead = file.Text;
        var secondRead = file.Text;

        secondRead.ShouldBeSameAs(firstRead);
    }

    [Fact]
    public void Edit_that_fails_its_anchor_check_plans_nothing()
    {
        var anchorError = Error.Failure("scaffold.anchor", "Program.cs must hold 'builder.AddPlatform();' once");
        var file = new TextFile("Program.cs", Encoding.UTF8.GetBytes("var builder = 1;\n"));

        var plannedEdit = file.PlanEdit(_ => anchorError);

        plannedEdit.ShouldBe(Result.Failure<FileEdit>(anchorError));
    }

    [Theory]
    [InlineData("<Project>\r\n</Project>\r\n", "\r\n")]
    [InlineData("<Project>\n</Project>\n", "\n")]
    public void New_line_is_the_one_the_file_uses(String text, String newLine)
    {
        var file = new TextFile("host.csproj", Encoding.UTF8.GetBytes(text));

        file.NewLine.ShouldBe(newLine);
    }
}
