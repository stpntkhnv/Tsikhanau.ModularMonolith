using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class HostProjectEditTests
{
    private const String ReferencesEnd = "  </ItemGroup>\n</Project>";

    private const String NotesInLowerCase =
        @"    <ProjectReference Include=""..\..\Modules\notes\"
        + @"Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj"" />";

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

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
