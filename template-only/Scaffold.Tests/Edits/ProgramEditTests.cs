using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Tests.Samples;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class ProgramEditTests
{
    private const String AddServicesBody = "static void AddServices(WebApplicationBuilder builder)\n{\n";

    private const String PlatformUsing = "using Acme.Shop.Bootstrapper.Platform;\n";

    private const String PlatformCall = "    builder.AddPlatform();\n";

    private const String NotesUsing = "using Acme.Shop.Modules.Notes;\n";

    private const String ModuleApiUsing = "using Tsikhanau.Monolith.Api.Modules;";

    private const String AddServicesAnchorError =
        $"{SampleApp.ProgramPath} must hold exactly one line "
        + "static void AddServices(WebApplicationBuilder builder) followed by a line {";

    [Fact]
    public void Module_call_goes_into_the_body_of_add_services_with_the_module_using()
    {
        var notes = SampleModules.Create("Notes");

        var edited = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), notes));

        edited.ShouldBe(
            """
            using Acme.Shop.Modules.Notes;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Hosting;

            return await MonolithHost.RunAsync(args, AddServices);

            static void AddServices(WebApplicationBuilder builder)
            {
                builder.AddModule<NotesModule>();
            }

            public partial class Program;

            """);
    }

    [Theory]
    [InlineData("Billing", "Billing", "Notes")]
    [InlineData("Tags", "Notes", "Tags")]
    public void Module_calls_in_add_services_stay_in_module_name_order(String second, String earlier, String later)
    {
        var withNotes = CreateProgram(
            ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), SampleModules.Create("Notes"))));

        var edited = ReadText(ProgramEdit.AddModule(withNotes, SampleModules.Create(second)));

        edited.ShouldContain($"{AddServicesBody}{BuildCall(earlier)}{BuildCall(later)}}}\n", Case.Sensitive);
    }

    [Fact]
    public void Program_of_core_102_gets_the_module_call_after_its_platform_call()
    {
        var notes = SampleModules.Create("Notes");

        var edited = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core102), notes));

        edited.ShouldBe(
            Insert(
                Insert(FrozenPrograms.Core102, PlatformUsing, NotesUsing + ModuleApiUsing + "\n"),
                PlatformCall,
                BuildCall("Notes")));
    }

    [Fact]
    public void Program_of_core_77_gets_the_module_call_after_its_platform_call()
    {
        var notes = SampleModules.Create("Notes");

        var edited = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core77), notes));

        edited.ShouldBe(
            Insert(
                Insert(FrozenPrograms.Core77, PlatformUsing, NotesUsing),
                $"    {PlatformCall}",
                $"    {BuildCall("Notes")}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("static void AddServices(WebApplicationBuilder builder) {\n")]
    [InlineData(AddServicesBody + "}\n\n" + AddServicesBody)]
    public void Program_without_one_add_services_body_is_refused(String addServicesBody)
    {
        var notes = SampleModules.Create("Notes");
        var program = CreateProgram(
            FrozenPrograms.Core103.Replace(AddServicesBody, addServicesBody, StringComparison.Ordinal));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe(AddServicesAnchorError);
    }

    [Fact]
    public void Program_of_core_102_with_two_platform_calls_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var program = CreateProgram(
            FrozenPrograms.Core102.Replace(PlatformCall, PlatformCall + PlatformCall, StringComparison.Ordinal));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold exactly one line builder.AddPlatform();");
    }

    [Fact]
    public void Using_goes_after_the_system_usings_and_in_order_among_the_others()
    {
        var notes = SampleModules.Create("Notes");
        var program = CreateProgram($"using System.Text;\n{FrozenPrograms.Core103}");

        var edited = ReadText(ProgramEdit.AddModule(program, notes));

        edited.ShouldStartWith(
            """
            using System.Text;
            using Acme.Shop.Modules.Notes;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Hosting;
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Using_of_an_app_that_sorts_after_the_core_goes_after_the_core_usings()
    {
        var notes = SampleModules.Create("Notes", appName: "Zeta");

        var edited = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), notes));

        edited.ShouldStartWith(
            """
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Hosting;
            using Zeta.Modules.Notes;

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Using_of_an_app_in_lower_case_sorts_ignoring_case_among_the_core_usings()
    {
        var notes = SampleModules.Create("Notes", appName: "trip_own");

        var edited = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), notes));

        edited.ShouldStartWith(
            """
            using trip_own.Modules.Notes;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Hosting;
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Second_module_keeps_the_one_using_of_the_module_api()
    {
        var notes = SampleModules.Create("Notes");
        var tags = SampleModules.Create("Tags");
        var withNotes = CreateProgram(ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), notes)));

        var edited = ReadText(ProgramEdit.AddModule(withNotes, tags));

        var moduleApiUsings = edited
            .Split('\n')
            .Count(line => line == ModuleApiUsing);

        moduleApiUsings.ShouldBe(1);
    }

    [Fact]
    public void Program_with_crlf_line_endings_keeps_them()
    {
        var notes = SampleModules.Create("Notes");
        var lfProgram = ReadText(ProgramEdit.AddModule(CreateProgram(FrozenPrograms.Core103), notes));
        var crlfProgram = CreateProgram(FrozenPrograms.Core103.ReplaceLineEndings("\r\n"));

        var edited = ReadText(ProgramEdit.AddModule(crlfProgram, notes));

        edited.ShouldBe(lfProgram.ReplaceLineEndings("\r\n"));
    }

    [Fact]
    public void Program_that_adds_the_module_already_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var program = CreateProgram(Insert(FrozenPrograms.Core103, AddServicesBody, BuildCall("Notes")));

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold no builder.AddModule<NotesModule>(); yet");
    }

    [Fact]
    public void Program_that_uses_the_module_namespace_already_is_refused()
    {
        var notes = SampleModules.Create("Notes");
        var program = CreateProgram(NotesUsing + FrozenPrograms.Core103);

        var message = ReadMessage(ProgramEdit.AddModule(program, notes));

        message.ShouldBe($"{SampleApp.ProgramPath} must hold no using Acme.Shop.Modules.Notes; yet");
    }

    private static TextFile CreateProgram(String text) => SampleModules.CreateFile(SampleApp.ProgramPath, text);

    private static String Insert(String text, String anchor, String addition) =>
        text.Replace(anchor, anchor + addition, StringComparison.Ordinal);

    private static String BuildCall(String name) => $"    builder.AddModule<{name}Module>();\n";

    private static String ReadText(Result<FileEdit> edit)
    {
        edit.IsSuccess.ShouldBeTrue(ReadMessage(edit));
        return Encoding.UTF8.GetString(edit.Value.Updated);
    }

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
