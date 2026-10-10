using System.Diagnostics;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class InternalCommandCommandTests : IAsyncLifetime
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String ModulesFolder = "src/Modules";

    private const String NotesProject = $"{ModulesFolder}/Notes/Acme.Shop.Modules.Notes";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String MailDigestCommand =
        "Notes/Acme.Shop.Modules.Notes/Features/MailDigest/MailDigestInternalCommand.cs";

    private const String DigestNamesPath = $"{NotesProject}/Features/MailDigest/DigestNames.cs";

    private const String SendDigestCommand = "feature Notes SendDigest --kind internal-command";

    private const String SendDigestTestFails =
        "the new test SendDigestTests.Send_digest_runs_when_the_queues_run fails with "
        + "\"SendDigest in Notes is not implemented\" until the handler does its work; next, run ./gate.sh\n";

    private const String SendDigestDone = $"scaffold: done; {SendDigestTestFails}";

    private const String CheckScaffold = "template-only/scripts/check-scaffold.sh";

    private const String NewTestReader = "new_test=$(sed -nE '";

    private const String NameIsStored =
        "the name is stored in the queue and must be unique in the module, so choose another feature name";

    private const String NamesItReads =
        "because it reads an internal command name only as a string literal or a string constant of the module; "
        + "the gate's internal command name rule checks the module's names";

    private const String DigestNames =
        """
        namespace Acme.Shop.Modules.Notes.Features.MailDigest;

        internal static class DigestNames
        {
            public const String Send = "send-digest";

            public const String Alias = Send;

            public const String Shared = "send-digest";
        }

        internal static class MailNames
        {
            public const String Shared = "mail-digest";

            internal static class Schedules
            {
                public const String Weekly = "send-digest";
            }
        }

        internal abstract class DigestNameBase
        {
            public const String Inherited = "send-digest";
        }

        internal sealed class InheritedNames : DigestNameBase;
        """;

    private readonly SampleApp _app = SampleApp.Create();

    private readonly StringWriter _output = new();

    private readonly StringWriter _error = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await AddModuleAsync("Notes");
        await AddModuleAsync("Tags");
    }

    public ValueTask DisposeAsync()
    {
        _output.Dispose();
        _error.Dispose();
        _app.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Internal_command_of_a_module_without_commands_writes_its_record_its_handler_and_its_test()
    {
        var exitCode = await RunAsync(SendDigestCommand);

        var status = ReadStatus();
        var output = _output.ToString();

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(SendDigestDone);
        status.ShouldBe(
            [
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs",
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommandHandler.cs",
                $"?? {NotesTests}/SendDigestTests.cs",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Internal_command_of_a_module_with_commands_writes_only_its_own_files()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");

        var exitCode = await RunAsync(SendDigestCommand);

        var status = ReadStatus();

        exitCode.ShouldBe(0, _error.ToString());
        status.ShouldBe(
            [
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs",
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommandHandler.cs",
                $"?? {NotesTests}/SendDigestTests.cs",
            ],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData(MailDigestCommand, """[InternalCommandName("send-digest")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandNameAttribute("send-digest")]""")]
    [InlineData(MailDigestCommand, """[Tsikhanau.Monolith.Application.Commands.InternalCommandName("send-digest")]""")]
    [InlineData(
        MailDigestCommand,
        """[global::Tsikhanau.Monolith.Application.Commands.InternalCommandNameAttribute("send-digest")]""")]
    [InlineData(MailDigestCommand, """[ContainsSecrets, InternalCommandName("send-digest")]""")]
    [InlineData(MailDigestCommand, "[ContainsSecrets]\n[InternalCommandName(\n    \"send-digest\")]")]
    [InlineData(MailDigestCommand, """[type: InternalCommandName ( name: "send-digest" )]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName(@"send-digest")]""")]
    [InlineData(MailDigestCommand, "[InternalCommandName(\"\"\"send-digest\"\"\")]")]
    [InlineData(MailDigestCommand, "[InternalCommandName(\"send\\u002Ddigest\")]")]
    [InlineData(MailDigestCommand, """[InternalCommandName(DigestNames.Send)]""")]
    [InlineData(
        MailDigestCommand,
        """[InternalCommandName(Acme.Shop.Modules.Notes.Features.MailDigest.DigestNames.Send)]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName(InheritedNames.Inherited)]""")]
    [InlineData(
        "Notes/Acme.Shop.Modules.Notes.Contracts/MailDigestInternalCommand.cs",
        """[InternalCommandName("send-digest")]""")]
    public async Task Name_taken_in_the_module_is_refused_however_the_attribute_is_written(
        String commandPath,
        String attributes)
    {
        _app.Write(DigestNamesPath, DigestNames);
        _app.Write($"{ModulesFolder}/{commandPath}", BuildInternalCommand(attributes));
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(SendDigestCommand);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: Notes already has an internal command named send-digest ({ModulesFolder}/{commandPath}); "
            + $"{NameIsStored}\n");

        after.ShouldBe(before);
    }

    [Fact]
    public async Task Name_is_the_feature_name_in_kebab_case_so_another_spelling_of_it_is_taken_too()
    {
        _app.Write(
            $"{NotesProject}/Features/Cleanup/PurgeHttpLogsInternalCommand.cs",
            BuildInternalCommand("""[InternalCommandName("purge-http-logs")]"""));

        _app.Commit();

        var exitCode = await RunAsync("feature Notes PurgeHTTPLogs --kind internal-command");

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: Notes already has an internal command named purge-http-logs "
            + $"({NotesProject}/Features/Cleanup/PurgeHttpLogsInternalCommand.cs); {NameIsStored}\n");
    }

    [Fact]
    public async Task Name_of_an_internal_command_does_not_stop_a_feature_of_another_kind()
    {
        _app.Write(
            $"{ModulesFolder}/{MailDigestCommand}",
            BuildInternalCommand("""[InternalCommandName("send-digest")]"""));

        _app.Write(
            $"{NotesProject}/Features/Mail/MailInternalCommand.cs",
            BuildInternalCommand("""[InternalCommandName(SharedNames.Mail)]"""));

        _app.Commit();

        var exitCode = await RunAsync("feature Notes SendDigest --kind command");

        exitCode.ShouldBe(0, _error.ToString());
    }

    [Fact]
    public async Task Every_file_that_holds_the_name_is_listed()
    {
        _app.Write(
            $"{ModulesFolder}/{MailDigestCommand}",
            BuildInternalCommand("""[InternalCommandName("send-digest")]"""));

        _app.Write(
            "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts/DigestInternalCommand.cs",
            BuildInternalCommand("""[InternalCommandName("send-digest")]"""));

        _app.Commit();

        var exitCode = await RunAsync(SendDigestCommand);

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: Notes already has an internal command named send-digest "
            + "(src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts/DigestInternalCommand.cs, "
            + $"{ModulesFolder}/{MailDigestCommand}); "
            + $"{NameIsStored}\n");
    }

    [Theory]
    [InlineData(MailDigestCommand, """// [InternalCommandName("send-digest")]""")]
    [InlineData(MailDigestCommand, "[Description(\"\"\"[InternalCommandName(\"send-digest\")]\"\"\")]")]
    [InlineData(MailDigestCommand, """[IntegrationEventName("send-digest")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName("send-digests")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName("Send-Digest")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName(nameof(MailDigestInternalCommand))]""")]
    [InlineData(
        "Tags/Acme.Shop.Modules.Tags/Features/MailDigest/MailDigestInternalCommand.cs",
        """[InternalCommandName("send-digest")]""")]
    public async Task Name_that_only_looks_taken_leaves_the_internal_command_free(String commandPath, String attributes)
    {
        _app.Write($"{ModulesFolder}/{commandPath}", BuildInternalCommand(attributes));
        _app.Commit();

        var exitCode = await RunAsync(SendDigestCommand);

        exitCode.ShouldBe(0, _error.ToString());
    }

    [Theory]
    [InlineData("""[InternalCommandName(SharedNames.Digest)]""", "[InternalCommandName(SharedNames.Digest)]")]
    [InlineData("""[InternalCommandName("send-" + "digest")]""", "[InternalCommandName(\"send-\" + \"digest\")]")]
    [InlineData("""[InternalCommandName(DigestNames.Alias)]""", "[InternalCommandName(DigestNames.Alias)]")]
    [InlineData("""[InternalCommandName(DigestNames.Shared)]""", "[InternalCommandName(DigestNames.Shared)]")]
    [InlineData("""[InternalCommandName(SharedNames.Send)]""", "[InternalCommandName(SharedNames.Send)]")]
    [InlineData(
        """[InternalCommandName(MailNames.Schedules.Weekly)]""",
        "[InternalCommandName(MailNames.Schedules.Weekly)]")]
    public async Task Name_scaffold_cannot_read_is_named_in_a_note_after_the_done_line_and_the_feature_is_written(
        String attributes,
        String unread)
    {
        _app.Write(DigestNamesPath, DigestNames);
        _app.Write($"{ModulesFolder}/{MailDigestCommand}", BuildInternalCommand(attributes));
        _app.Commit();

        var exitCode = await RunAsync(SendDigestCommand);

        var status = ReadStatus();
        var output = _output.ToString();

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            SendDigestDone
            + $"scaffold: note: it did not compare send-digest with {unread} ({ModulesFolder}/{MailDigestCommand}:5), "
            + $"{NamesItReads}\n");

        status.ShouldBe(
            [
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs",
                $"?? {NotesProject}/Features/SendDigest/SendDigestInternalCommandHandler.cs",
                $"?? {NotesTests}/SendDigestTests.cs",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task New_test_is_read_from_the_done_line_as_check_scaffold_reads_it_when_a_note_follows()
    {
        _app.Write(
            $"{ModulesFolder}/{MailDigestCommand}",
            BuildInternalCommand("""[InternalCommandName(SharedNames.Digest)]"""));

        _app.Commit();

        var exitCode = await RunAsync(SendDigestCommand);

        var output = _output.ToString();
        var newTest = await ReadNewTestAsCheckScaffoldDoesAsync(output);

        exitCode.ShouldBe(0, _error.ToString());
        newTest.ShouldBe("SendDigestTests.Send_digest_runs_when_the_queues_run\n");
        output.ShouldContain("\nscaffold: note: it did not compare send-digest with ", Case.Sensitive);
    }

    [Fact]
    public async Task Name_held_twice_in_one_file_lists_the_file_once()
    {
        _app.Write(
            $"{ModulesFolder}/{MailDigestCommand}",
            BuildInternalCommand(
                """
                [InternalCommandName("send-digest")]
                internal sealed record WeeklyDigestInternalCommand(Guid Id) : InternalCommand(Id);

                [InternalCommandName("send-digest")]
                """));

        _app.Commit();

        var exitCode = await RunAsync(SendDigestCommand);

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: Notes already has an internal command named send-digest ({ModulesFolder}/{MailDigestCommand}); "
            + $"{NameIsStored}\n");
    }

    [Theory]
    [InlineData(
        "feature Notes Sweep --kind internal-command --endpoint delete",
        "an internal command has no endpoint: a handler of the module schedules it and the queue runs it")]
    [InlineData(
        "feature Notes Sweep --kind internal-command --endpoint get",
        "an internal command has no endpoint: a handler of the module schedules it and the queue runs it")]
    [InlineData(
        "feature Notes Sweep --kind internal-command --event NoteCreatedDomainEvent",
        "--event belongs to --kind event-handler: an internal command handles no event")]
    public async Task Internal_command_with_an_endpoint_or_an_event_is_refused_before_any_write(
        String commandLine,
        String message)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(commandLine);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: {message}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Internal_command_scaffolded_twice_is_refused_with_its_files_and_its_taken_name()
    {
        await RunAndCommitAsync(SendDigestCommand);
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(SendDigestCommand);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: feature SendDigest of Notes already exists: {NotesProject}/Features/SendDigest, "
            + $"{NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs, "
            + $"{NotesProject}/Features/SendDigest/SendDigestInternalCommandHandler.cs, "
            + $"{NotesTests}/SendDigestTests.cs; "
            + "Notes already has an internal command named send-digest "
            + $"({NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs); {NameIsStored}\n");

        after.ShouldBe(before);
    }

    [Theory]
    [InlineData(1, $"{NotesProject}/Features/SendDigest/SendDigestInternalCommand.cs")]
    [InlineData(2, $"{NotesProject}/Features/SendDigest/SendDigestInternalCommandHandler.cs")]
    [InlineData(3, $"{NotesTests}/SendDigestTests.cs")]
    public async Task Failed_write_of_an_internal_command_leaves_the_tree_as_it_was(
        Int32 failingWrite,
        String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(new FailingFileSystem(_app.Root, failingWrite: failingWrite), SendDigestCommand);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

    private static String BuildInternalCommand(String attributes) =>
        $"""
         using Tsikhanau.Monolith.Application.Commands;

         namespace Acme.Shop.Modules.Notes.Features.MailDigest;

         {attributes}
         internal sealed record MailDigestInternalCommand(Guid Id) : InternalCommand(Id);
         """;

    private async Task AddModuleAsync(String name)
    {
        var exitCode = await new ScaffoldTool(
                _app.FileSystem,
                FakeProcessRunner.Succeeding(),
                TextWriter.Null,
                TextWriter.Null)
            .RunAsync(["module", name], CancellationToken);

        exitCode.ShouldBe(0);
        _app.Commit();
    }

    private async Task RunAndCommitAsync(String commandLine)
    {
        var exitCode = await RunAsync(commandLine);

        exitCode.ShouldBe(0, _error.ToString());
        _app.Commit();
        _output
            .GetStringBuilder()
            .Clear();
    }

    private Task<Int32> RunAsync(String commandLine) => RunAsync(_app.FileSystem, commandLine);

    private Task<Int32> RunAsync(IFileSystem fileSystem, String commandLine) =>
        new ScaffoldTool(fileSystem, FakeProcessRunner.Succeeding(), _output, _error)
            .RunAsync(commandLine.Split(' '), CancellationToken);

    private static async Task<String> ReadNewTestAsCheckScaffoldDoesAsync(String output)
    {
        var startInfo = new ProcessStartInfo("sed", ["-nE", ReadNewTestExpression()])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };

        using var sed = Process.Start(startInfo) ?? throw new InvalidOperationException("cannot start sed");
        await sed.StandardInput.WriteAsync(output.AsMemory(), CancellationToken);
        sed.StandardInput.Close();
        var newTest = await sed.StandardOutput.ReadToEndAsync(CancellationToken);
        await sed.WaitForExitAsync(CancellationToken);
        return newTest;
    }

    private static String ReadNewTestExpression() =>
        File
            .ReadLines(Path.Combine(TemplateFolder.Root, CheckScaffold))
            .Single(line => line.Contains(NewTestReader, StringComparison.Ordinal))
            .Split('\'')[1];

    private String[] ReadStatus() =>
        _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
