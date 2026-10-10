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

    private const String ArchiveNamesPath = $"{NotesProject}/Features/Archive/ArchiveNames.cs";

    private const String SendDigestCommand = "feature Notes SendDigest --kind internal-command";

    private const String SendDigestDone =
        "scaffold: done; the new test SendDigestTests.Send_digest_runs_when_the_queues_run fails with "
        + "\"SendDigest in Notes is not implemented\" until the handler does its work; next, run ./gate.sh\n";

    private const String NameIsStored =
        "the name is stored in the queue and must be unique in the module, so choose another feature name";

    private const String ReadableForms =
        "it reads string literals, their concatenations and interpolations, nameof and the string constants "
        + "declared in the module, so write the name in one of these forms";

    private const String DigestNames =
        """
        namespace Acme.Shop.Modules.Notes.Features.MailDigest;

        internal static class DigestNames
        {
            public const String Prefix = "send";

            public const String Send = Prefix + "-digest";
        }
        """;

    private const String ArchiveNames =
        """
        namespace Acme.Shop.Modules.Notes.Features.Archive;

        internal static class ArchiveNames
        {
            public const String Prefix = "archive";

            public const String Send = Prefix + "-digest";
        }
        """;

    private const String LoopingNames =
        """
        namespace Acme.Shop.Modules.Notes.Features.MailDigest;

        internal static class LoopingNames
        {
            public const String First = Second;

            public const String Second = First;
        }
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
    [InlineData(MailDigestCommand, """[InternalCommandName(("send-" + "dig") + "est")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName($"{DigestNames.Prefix}-{"digest"}")]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName(DigestNames.Send)]""")]
    [InlineData(MailDigestCommand, """[InternalCommandName(Features.MailDigest.DigestNames.Send)]""")]
    [InlineData(
        "Notes/Acme.Shop.Modules.Notes.Contracts/MailDigestInternalCommand.cs",
        """[InternalCommandName("send-digest")]""")]
    public async Task Name_taken_in_the_module_is_refused_however_the_attribute_is_written(
        String commandPath,
        String attributes)
    {
        _app.Write(DigestNamesPath, DigestNames);
        _app.Write(ArchiveNamesPath, ArchiveNames);
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
    [InlineData("""[InternalCommandName(DigestNames.Archive)]""", "[InternalCommandName(DigestNames.Archive)]")]
    [InlineData("""[InternalCommandName(Prefix)]""", "[InternalCommandName(Prefix)]")]
    [InlineData("""[InternalCommandName(Names.Build("send"))]""", "[InternalCommandName(Names.Build(\"send\"))]")]
    [InlineData("""[InternalCommandName(LoopingNames.First)]""", "[InternalCommandName(LoopingNames.First)]")]
    [InlineData(
        """[InternalCommandName($"{DigestNames.Prefix,4}-digest")]""",
        "[InternalCommandName($\"{DigestNames.Prefix, 4}-digest\")]")]
    public async Task Name_scaffold_cannot_read_is_refused_because_the_new_name_may_be_taken(
        String attributes,
        String unreadable)
    {
        _app.Write(DigestNamesPath, DigestNames);
        _app.Write(ArchiveNamesPath, ArchiveNames);

        _app.Write($"{NotesProject}/Features/MailDigest/LoopingNames.cs", LoopingNames);
        _app.Write($"{ModulesFolder}/{MailDigestCommand}", BuildInternalCommand(attributes));
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(SendDigestCommand);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: cannot tell whether send-digest is free in Notes: scaffold cannot read the internal command "
            + $"name of {unreadable} ({ModulesFolder}/{MailDigestCommand}:5); {ReadableForms}\n");

        after.ShouldBe(before);
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

    private String[] ReadStatus() =>
        _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
