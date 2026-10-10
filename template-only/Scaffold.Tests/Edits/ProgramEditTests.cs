using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class ProgramEditTests
{
    private const String PlatformCall = "        builder.AddPlatform();\n";

    private const String PlatformUsing = "using Acme.Shop.Bootstrapper.Platform;\n";

    [Fact]
    public void Using_goes_after_the_system_usings_and_in_order_among_the_others()
    {
        var notes = SampleModules.Create("Notes");
        var program = SampleModules.CreateFile(SampleApp.ProgramPath, $"using System.Text;\n{SampleApp.Program}");

        var edited = ReadText(ProgramEdit.AddModule(program, notes));

        edited.ShouldStartWith(
            """
            using System.Text;
            using Acme.Shop.Bootstrapper.Platform;
            using Acme.Shop.Modules.Notes;
            using Tsikhanau.Monolith.Api.Modules;
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Using_of_an_app_that_sorts_after_the_core_goes_after_the_core_usings()
    {
        var notes = SampleModules.Create("Notes", appName: "Zeta");

        var edited = ReadText(ProgramEdit.AddModule(notes.App.Program, notes));

        edited.ShouldStartWith(
            """
            using Zeta.Bootstrapper.Platform;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Hosting;
            using Tsikhanau.Monolith.Infrastructure.Diagnostics;
            using Tsikhanau.Monolith.Infrastructure.Modules;
            using Zeta.Modules.Notes;

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Program_with_crlf_line_endings_keeps_them()
    {
        var notes = SampleModules.Create("Notes");
        var lfProgram = ReadText(ProgramEdit.AddModule(notes.App.Program, notes));
        var crlfProgram = SampleModules.CreateFile(SampleApp.ProgramPath, SampleApp.Program.ReplaceLineEndings("\r\n"));

        var edited = ReadText(ProgramEdit.AddModule(crlfProgram, notes));

        edited.ShouldBe(lfProgram.ReplaceLineEndings("\r\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(PlatformCall + PlatformCall)]
    public void Program_without_exactly_one_platform_call_is_refused(String platformCalls)
    {
        var notes = SampleModules.Create("Notes");
        var program = SampleModules.CreateFile(
            SampleApp.ProgramPath,
            SampleApp.Program.Replace(PlatformCall, platformCalls, StringComparison.Ordinal));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold exactly one line builder.AddPlatform();");
    }

    [Fact]
    public void Program_that_adds_the_module_already_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var program = SampleModules.CreateFile(
            SampleApp.ProgramPath,
            SampleApp.Program.Replace(
                PlatformCall,
                $"{PlatformCall}        builder.AddModule<NotesModule>();\n",
                StringComparison.Ordinal));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold no builder.AddModule<NotesModule>(); yet");
    }

    [Fact]
    public void Program_that_uses_the_module_namespace_already_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var program = SampleModules.CreateFile(
            SampleApp.ProgramPath,
            SampleApp.Program.Replace(
                PlatformUsing,
                $"{PlatformUsing}using Acme.Shop.Modules.Notes;\n",
                StringComparison.Ordinal));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold no using Acme.Shop.Modules.Notes; yet");
    }

    private static String ReadText(Result<FileEdit> edit)
    {
        edit.IsSuccess.ShouldBeTrue(ReadMessage(edit));
        return Encoding.UTF8.GetString(edit.Value.Updated);
    }

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
