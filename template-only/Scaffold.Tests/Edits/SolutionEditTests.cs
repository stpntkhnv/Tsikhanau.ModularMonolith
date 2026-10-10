using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class SolutionEditTests
{
    private const String ModulesFolder = "  <Folder Name=\"/src/Modules/\" />";

    private const String NotesInLowerCase = "  <Folder Name=\"/src/Modules/notes/\" />";

    [Fact]
    public void Solution_with_crlf_line_endings_keeps_them()
    {
        var notes = SampleModules.Create("Notes");
        var lfSolution = ReadText(SolutionEdit.AddModule(notes.App.Solution, notes));
        var crlfSolution = SampleModules.CreateFile(
            SampleApp.SolutionPath,
            SampleApp.Solution.ReplaceLineEndings("\r\n"));

        var solution = ReadText(SolutionEdit.AddModule(crlfSolution, notes));

        solution.ShouldBe(lfSolution.ReplaceLineEndings("\r\n"));
    }

    [Fact]
    public void Solution_without_its_end_line_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var solution = SampleModules.CreateFile(
            SampleApp.SolutionPath,
            SampleApp.Solution.Replace("</Solution>", String.Empty, StringComparison.Ordinal));

        var message = ReadMessage(SolutionEdit.AddModule(solution, notes));

        message.ShouldBe("Acme.Shop.slnx must hold one </Solution> line");
    }

    [Fact]
    public void Folder_of_the_module_in_another_letter_case_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var solution = SampleModules.CreateFile(
            SampleApp.SolutionPath,
            SampleApp.Solution.Replace(
                ModulesFolder,
                $"{ModulesFolder}\n{NotesInLowerCase}",
                StringComparison.Ordinal));

        var message = ReadMessage(SolutionEdit.AddModule(solution, notes));

        message.ShouldBe("Acme.Shop.slnx must hold no folder /src/Modules/Notes/ yet");
    }

    private static String ReadText(Result<FileEdit> edit)
    {
        edit.IsSuccess.ShouldBeTrue(ReadMessage(edit));
        return Encoding.UTF8.GetString(edit.Value.Updated);
    }

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
