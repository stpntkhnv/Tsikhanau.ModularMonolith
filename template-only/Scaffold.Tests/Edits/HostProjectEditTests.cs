using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Tests.Samples;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class HostProjectEditTests
{
    private const String ReferencesEnd = "  </ItemGroup>\n</Project>";

    private const String NotesInLowerCase =
        @"    <ProjectReference Include=""..\..\Modules\notes\"
        + @"Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj"" />";

    private const String CoreApiReference =
        @"    <ProjectReference Include=""..\..\Core\Api\Tsikhanau.Monolith.Api.csproj"" />";

    private const String NotesReference =
        @"    <ProjectReference Include=""..\..\Modules\Notes\"
        + @"Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj"" />";

    private const String MixedReferencesStart = "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n  <ItemGroup>\r\n";

    private const String MixedReferencesEnd = "\r\n  </ItemGroup>\n</Project>";

    private static readonly Byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void Host_project_with_a_byte_order_mark_mixed_line_endings_and_no_final_line_break_keeps_them()
    {
        var notes = SampleModules.Create("Notes");
        var hostProject = new TextFile(
            SampleApp.HostProjectPath,
            WithByteOrderMark($"{MixedReferencesStart}{CoreApiReference}{MixedReferencesEnd}"));

        var edit = HostProjectEdit.AddModule(hostProject, notes);

        edit.IsSuccess.ShouldBeTrue(ReadMessage(edit));
        edit.Value.Original.ShouldBe(
            WithByteOrderMark($"{MixedReferencesStart}{CoreApiReference}{MixedReferencesEnd}"));

        edit.Value.Updated.ShouldBe(
            WithByteOrderMark($"{MixedReferencesStart}{CoreApiReference}\r\n{NotesReference}{MixedReferencesEnd}"));
    }

    [Fact]
    public void Host_project_without_project_references_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var hostProject = SampleModules.CreateFile(SampleApp.HostProjectPath, "<Project>\n</Project>\n");

        var message = ReadMessage(HostProjectEdit.AddModule(hostProject, notes));

        message.ShouldBe($"{SampleApp.HostProjectPath} must hold a ProjectReference to the core");
    }

    [Fact]
    public void Reference_to_the_module_in_another_letter_case_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var hostProject = SampleModules.CreateFile(
            SampleApp.HostProjectPath,
            SampleApp.HostProject.Replace(
                ReferencesEnd,
                $"{NotesInLowerCase}\n{ReferencesEnd}",
                StringComparison.Ordinal));

        var message = ReadMessage(HostProjectEdit.AddModule(hostProject, notes));

        message.ShouldBe(
            $"{SampleApp.HostProjectPath} must hold no reference to "
            + @"..\..\Modules\Notes\Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj yet");
    }

    private static Byte[] WithByteOrderMark(String text) => [.. ByteOrderMark, .. Encoding.UTF8.GetBytes(text)];

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
