using System.Diagnostics;
using System.Text;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Checks;

public sealed class CheckFeatureFailuresTests : IDisposable
{
    private const String Script = "template-only/scripts/check-feature-failures.sh";

    private const String RecordedGates = "template-only/Scaffold.Tests/Checks/GateOutputs";

    private const String Prefix = "check-feature-failures: ";

    private const String RecordedSkips = "0";

    private const Int32 FailedGateExitCode = 2;

    private const String Tests = "Acme.Shop.IntegrationTests.Modules";

    private const String ArchiveNoteTest = "Notes.ArchiveNoteTests.Archive_note_succeeds_through_the_facade";

    private const String RecountTagsTest = "Tags.RecountTagsTests.Recount_tags_runs_when_the_queues_run";

    private const String GetTagTest = "GetTagTests.Get_tag_succeeds_through_the_query_facade";

    private const String DomainEventMethod = "Note_created_domain_event_is_handled";

    private const String IntegrationEventMethod = "Note_created_integration_event_is_handled_through_the_inbox";

    private const String NoteCreatedInNotes = $"Notes.NoteCreatedTests.{DomainEventMethod}";

    private const String NoteCreatedInTags = $"Tags.NoteCreatedTests.{IntegrationEventMethod}";

    private const String ArchiveNote = $"{Tests}.{ArchiveNoteTest}";

    private const String RecountTags = $"{Tests}.{RecountTagsTest}";

    private const String ArchiveNoteText = "ArchiveNote in Notes is not implemented";

    private const String RecountTagsText = "RecountTags in Tags is not implemented";

    private const String Solution =
        """
        <Solution>
          <Folder Name="/src/Bootstrapper/">
            <Project Path="src/Bootstrapper/Acme.Shop.Bootstrapper/Acme.Shop.Bootstrapper.csproj" />
          </Folder>
          <Folder Name="/src/Tests/">
            <Project Path="src/Tests/ArchTests/Acme.Shop.ArchTests.csproj" />
            <Project Path="src/Tests/IntegrationTests/Acme.Shop.IntegrationTests.csproj" />
            <Project Path="src/Tests/Testing.App/Acme.Shop.Testing.App.csproj" />
            <Project Path="src/Tests/UnitTests/Acme.Shop.UnitTests.csproj" />
          </Folder>
        </Solution>
        """;

    private static readonly String[] ScaffoldedTests =
    [
        ArchiveNoteTest,
        "Notes.CreateNoteTests.Create_note_answers_no_content",
        "Notes.RenameNoteTests.Rename_note_answers_no_content",
        "Notes.DeleteNoteTests.Delete_note_answers_no_content",
        NoteCreatedInTags,
        "Notes.CountNotesTests.Count_notes_succeeds_through_the_facade",
        "Notes.GetNoteSummaryTests.Get_note_summary_answers_ok",
        $"Tags.{GetTagTest}",
        NoteCreatedInNotes,
        RecountTagsTest,
    ];

    private static readonly String GreenGate = ReadRecordedGate("green.txt");

    private static readonly String FailedGate = ReadRecordedGate("ten-features.txt");

    private static readonly String CrashedGate = ReadRecordedGate("crashed-app.txt");

    private readonly DirectoryInfo _app = Directory.CreateTempSubdirectory("check-feature-failures-");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _app.Delete(recursive: true);

    [Fact]
    public async Task Gate_that_fails_exactly_on_the_named_tests_with_their_texts_passes()
    {
        WriteGate(FailedGate, FailedGateExitCode);

        var check = await RunAsync(ScaffoldedTests);

        var failedTests = ScaffoldedTests
            .Order(StringComparer.Ordinal)
            .Select(name => $"  {Tests}.{name}\n");

        check.ExitCode.ShouldBe(0, check.Error);
        check.Output.ShouldBe(
            $"{Prefix}build without warnings; exactly the 10 named tests failed, each with its feature's "
            + "'<Feature> in <Module> is not implemented':\n"
            + String.Concat(failedTests)
            + $"{Prefix}skipped 0 as the step expects and .gate/skips records; every other test passed\n");
    }

    [Fact]
    public async Task Green_gate_passes_when_no_test_is_named()
    {
        WriteGate(GreenGate, 0);

        var check = await RunAsync([]);

        check.ExitCode.ShouldBe(0, check.Error);
        check.Output.ShouldBe(
            $"{Prefix}build without warnings; gate green, as expected with no test named; "
            + "skipped 0 as the step expects\n");
    }

    [Fact]
    public async Task Failure_with_another_text_is_refused()
    {
        WriteGate(FailedGateWith(ArchiveNoteText, "ArchiveNote in Notes broke"));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, $"{ArchiveNote} failed without '{ArchiveNoteText}':");
        check.ErrorLines.ShouldContain("  Failure([ArchiveNote.NotImplemented] ArchiveNote in Notes broke)");
    }

    [Fact]
    public async Task Text_outside_the_failure_of_the_test_does_not_count()
    {
        WriteGate(FailedGateWith($"{RecountTagsText}. A test", "RecountTags in Tags broke. A test"));

        var check = await RunAsync(ScaffoldedTests);

        FailedGate.ShouldContain($"Message failed: RecountTags.NotImplemented: {RecountTagsText}\n", Case.Sensitive);
        ShouldBeRefused(check, $"{RecountTags} failed without '{RecountTagsText}':");
    }

    [Fact]
    public async Task Text_of_a_not_implemented_exception_is_refused()
    {
        WriteGate(FailedGateWith(ArchiveNoteText, "The method or operation is not implemented."));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, $"{ArchiveNote} failed without '{ArchiveNoteText}':");
    }

    [Fact]
    public async Task Failed_test_left_out_of_the_names_is_refused()
    {
        WriteGate(FailedGate, FailedGateExitCode);

        var check = await RunAsync(ScaffoldedTests.Where(name => name != RecountTagsTest));

        ShouldBeRefused(check, $"{RecountTags} failed, but it is not among the expected tests");
    }

    [Fact]
    public async Task Named_test_that_did_not_fail_is_refused()
    {
        WriteGate(FailedGate, FailedGateExitCode);

        var check = await RunAsync([.. ScaffoldedTests, "Notes.PinNoteTests.Pin_note_succeeds_through_the_facade"]);

        ShouldBeRefused(
            check,
            "Notes.PinNoteTests.Pin_note_succeeds_through_the_facade was expected to fail, "
            + "but no failed test has that name");
    }

    [Fact]
    public async Task Name_that_matches_two_failed_tests_is_refused()
    {
        const String DomainEventTest = $"NoteCreatedTests.{DomainEventMethod}";
        WriteGate(FailedGateWith(IntegrationEventMethod, DomainEventMethod));

        var check = await RunAsync(
            ScaffoldedTests
                .Select(name => name == NoteCreatedInNotes ? DomainEventTest : name)
                .Select(name => name == NoteCreatedInTags ? $"Tags.{DomainEventTest}" : name));

        ShouldBeRefused(
            check,
            $"{DomainEventTest} names 2 failed tests ({Tests}.Notes.{DomainEventTest} {Tests}.Tags.{DomainEventTest}); "
            + "give more of the name");
    }

    [Fact]
    public async Task Green_gate_is_refused_when_tests_are_named()
    {
        WriteGate(GreenGate, 0);

        var check = await RunAsync([NoteCreatedInTags]);

        ShouldBeRefused(check, $"the gate is green, but these tests should fail: {NoteCreatedInTags}");
    }

    [Fact]
    public async Task Failed_gate_is_refused_when_no_test_is_named()
    {
        WriteGate(FailedGate, FailedGateExitCode);

        var check = await RunAsync([]);

        check.ExitCode.ShouldBe(1);
        check.ErrorLines[0].ShouldStartWith(
            $"{Prefix}no feature test is named, but the gate failed (exit 2); failed tests: {ArchiveNote} ",
            Case.Sensitive);
    }

    [Fact]
    public async Task Skipped_count_that_differs_from_the_step_is_refused()
    {
        WriteGate(FailedGate, FailedGateExitCode, skips: "1\n");

        var check = await RunAsync(ScaffoldedTests, skips: "1");

        ShouldBeRefused(check, "skipped tests: the run skipped 0, the step expects 1");
    }

    [Fact]
    public async Task Skip_file_that_differs_from_the_step_is_refused()
    {
        WriteGate(FailedGate, FailedGateExitCode, skips: "1\n");

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, "skipped tests: .gate/skips holds 1, the step expects 0");
    }

    [Fact]
    public async Task Green_gate_is_refused_when_it_skips_another_count_than_the_step()
    {
        WriteGate(GreenGate, 0, skips: "1\n");

        var check = await RunAsync([], skips: "1");

        ShouldBeRefused(check, "skipped tests: the run skipped 0, the step expects 1");
    }

    [Fact]
    public async Task Build_warnings_are_refused()
    {
        WriteGate(FailedGateWith("    0 Warning(s)", "    3 Warning(s)"));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, "the build reported warnings: 3");
    }

    [Fact]
    public async Task Gate_without_a_build_summary_is_refused()
    {
        WriteGate(FailedGateWith("    0 Warning(s)\n", ""));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, "the gate printed no build summary ('N Warning(s)')");
    }

    [Fact]
    public async Task Summary_with_two_failed_counts_is_refused()
    {
        WriteGate(FailedGate + FailedGate[FailedGate.IndexOf("Test run summary:", StringComparison.Ordinal)..]);

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, "cannot read the failed count; the test summary must hold one 'failed: N' line");
    }

    [Fact]
    public async Task Summary_without_a_skipped_count_is_refused()
    {
        WriteGate(FailedGateWith("  skipped: 0\n", ""));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(check, "cannot read the skipped count; the test summary must hold one 'skipped: N' line");
    }

    [Fact]
    public async Task Gate_that_stops_before_the_tests_is_refused()
    {
        WriteGate(FailedGate[..FailedGate.IndexOf("Running tests from", StringComparison.Ordinal)], 1);

        var check = await RunAsync(ScaffoldedTests);

        check.ExitCode.ShouldBe(1);
        check.ErrorLines[^2].ShouldBe($"{Prefix}the gate stopped before the tests ran (exit 1)");
    }

    [Fact]
    public async Task Summary_that_counts_more_failures_than_the_output_names_is_refused()
    {
        WriteGate(FailedGateWith("  failed: 10\n", "  failed: 11\n"));

        var check = await RunAsync(ScaffoldedTests);

        check.ExitCode.ShouldBe(1);
        check.ErrorLines[0].ShouldStartWith(
            $"{Prefix}the summary counts 11 failed tests, but the output names 10: {ArchiveNote} ",
            Case.Sensitive);
    }

    [Fact]
    public async Task Failed_test_outside_a_module_folder_is_refused()
    {
        WriteGate(FailedGateWith($"{Tests}.Tags.{GetTagTest}", $"Acme.Shop.IntegrationTests.{GetTagTest}"));

        var check = await RunAsync(ScaffoldedTests.Select(name => name == $"Tags.{GetTagTest}" ? GetTagTest : name));

        ShouldBeRefused(
            check,
            $"Acme.Shop.IntegrationTests.{GetTagTest} is not a scaffolded feature test "
            + "(<App>.IntegrationTests.Modules.<Module>.<Feature>Tests.<Method>)");
    }

    [Fact]
    public async Task Test_app_without_a_summary_is_refused()
    {
        WriteGate(FailedGateWithout("Acme.Shop.UnitTests"));

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(
            check,
            "no test summary line for Acme.Shop.UnitTests; every test project of Acme.Shop.slnx must report one");
    }

    [Fact]
    public async Task Crashed_test_app_is_refused()
    {
        WriteGate(CrashedGate);

        var check = await RunAsync(ScaffoldedTests);

        ShouldBeRefused(
            check,
            "Acme.Shop.ArchTests has no failed test, so it must pass, but its summary says "
            + "'Zero tests ran (1s 391ms)'");

        check.ErrorLines[1].ShouldBe(
            $"{Prefix}Acme.Shop.ArchTests ended with exit code 134, not 2: its test process failed (aborted or "
            + "crashed), not only its tests");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Folder_without_a_gate_gets_the_usage(Boolean namesAFolder)
    {
        var check = await RunScriptAsync(namesAFolder ? [_app.FullName] : []);

        check.ExitCode.ShouldBe(2);
        check.ErrorLines[0].ShouldBe($"usage: {Script} <app folder> <skips> [<Test.Name> ...]");
    }

    [Fact]
    public async Task Names_without_a_skip_count_get_the_usage()
    {
        WriteGate(FailedGate, FailedGateExitCode);

        var check = await RunScriptAsync([_app.FullName, .. ScaffoldedTests]);

        check.ExitCode.ShouldBe(2);
        check.ErrorLines[0].ShouldBe($"usage: {Script} <app folder> <skips> [<Test.Name> ...]");
    }

    private static String ReadRecordedGate(String fileName) =>
        File.ReadAllText(Path.Combine(TemplateFolder.Root, RecordedGates, fileName));

    private void WriteGate(String output) => WriteGate(output, FailedGateExitCode);

    private static String FailedGateWith(String text, String replacement)
    {
        FailedGate.ShouldContain(text, Case.Sensitive);
        return FailedGate.Replace(text, replacement, StringComparison.Ordinal);
    }

    private static String FailedGateWithout(String testApp) =>
        String.Join(
            '\n',
            FailedGate
                .Split('\n')
                .Where(line => !line.Contains($"/{testApp}.dll (", StringComparison.Ordinal)));

    private void WriteGate(String output, Int32 exitCode, String skips = "0\n")
    {
        File.WriteAllText(Path.Combine(_app.FullName, "Acme.Shop.slnx"), Solution);
        File.WriteAllText(Path.Combine(_app.FullName, "gate-output.txt"), output);
        File.WriteAllText(Path.Combine(_app.FullName, "gate.sh"), $"cat gate-output.txt\nexit {exitCode}\n");
        Directory.CreateDirectory(Path.Combine(_app.FullName, ".gate"));
        File.WriteAllText(Path.Combine(_app.FullName, ".gate", "skips"), skips);
    }

    private void ShouldBeRefused(CheckRun check, String problem)
    {
        check.ExitCode.ShouldBe(1, check.Output);
        check.ErrorLines[0].ShouldBe($"{Prefix}{problem}");
        check.ErrorLines[^1].ShouldStartWith($"{Prefix}the gate's output is kept in {_app.FullName}/", Case.Sensitive);
    }

    private Task<CheckRun> RunAsync(IEnumerable<String> names, String skips = RecordedSkips) =>
        RunScriptAsync([_app.FullName, skips, .. names]);

    private async Task<CheckRun> RunScriptAsync(IReadOnlyList<String> arguments)
    {
        using var process = Process.Start(CreateStartInfo(arguments))
            ?? throw new InvalidOperationException("cannot start bash");

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken);
        var error = process.StandardError.ReadToEndAsync(CancellationToken);
        await process.WaitForExitAsync(CancellationToken);

        return new CheckRun(process.ExitCode, await output, await error);
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<String> arguments) =>
        new("bash", [Path.Combine(TemplateFolder.Root, Script), .. arguments])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            Environment = { ["TMPDIR"] = _app.FullName },
        };
}
