using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class FeatureCommandTests : IAsyncLifetime
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String NotesProject = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String TagsProject = "src/Modules/Tags/Acme.Shop.Modules.Tags";

    private const String NotesContracts = "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts";

    private const String TagsContracts = "src/Modules/Tags/Acme.Shop.Modules.Tags.Contracts";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String TagsTests = "src/Tests/IntegrationTests/Modules/Tags";

    private const String NotesFacadeContract = $"{NotesProject}/INotesModule.cs";

    private const String NotesFacadeClass = $"{NotesProject}/Infrastructure/NotesModuleFacade.cs";

    private const String NotesCommandMarker = $"{NotesProject}/INotesCommand.cs";

    private const String NotesQueryMarker = $"{NotesContracts}/INotesQuery.cs";

    private const String NotesQueryFacade = $"{NotesContracts}/INotesQueryFacade.cs";

    private const String TagsFacadeContract = $"{TagsProject}/ITagsModule.cs";

    private const String TagsFacadeClass = $"{TagsProject}/Infrastructure/TagsModuleFacade.cs";

    private const String GetNoteSummaryFolder = $"{NotesProject}/Features/GetNoteSummary";

    private const String CreateNoteFolder = $"{NotesProject}/Features/CreateNote";

    private const String HandChangedFacade =
        " is not the file scaffold writes for this module, so scaffold cannot add ExecuteCommandAsync over "
        + "INotesCommand to it; restore it, or add INotesCommand and ExecuteCommandAsync by hand "
        + "and run scaffold again";

    private const String HandChangedFacadeForQueries =
        " is not the file scaffold writes for this module, so scaffold cannot add ExecuteQueryAsync over "
        + "INotesQuery to it; restore it, or add INotesQuery, INotesQueryFacade and ExecuteQueryAsync by hand "
        + "and run scaffold again";

    private const String FacadeContractWithCommands =
        """
        using Tsikhanau.Monolith.Application.Modules;
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes;

        internal interface INotesModule : IModuleFacade
        {
            Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                INotesCommand<TResult> command,
                CancellationToken cancellationToken)
                where TResult : notnull;
        }

        """;

    private const String FacadeClassWithCommands =
        """
        using Microsoft.Extensions.DependencyInjection;
        using Tsikhanau.Monolith.Infrastructure.Dispatching;
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes.Infrastructure;

        internal sealed class NotesModuleFacade(IServiceScopeFactory serviceScopeFactory)
            : ModuleFacade<INotesModule>(serviceScopeFactory), INotesModule
        {
            public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                INotesCommand<TResult> command,
                CancellationToken cancellationToken)
                where TResult : notnull =>
                SendAsync(command, cancellationToken);
        }

        """;

    private const String FacadeContractWithCommandsAndQueries =
        """
        using Acme.Shop.Modules.Notes.Contracts;
        using Tsikhanau.Monolith.Application.Modules;
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes;

        internal interface INotesModule : INotesQueryFacade, IModuleFacade
        {
            Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                INotesCommand<TResult> command,
                CancellationToken cancellationToken)
                where TResult : notnull;
        }

        """;

    private const String FacadeClassWithCommandsAndQueries =
        """
        using Acme.Shop.Modules.Notes.Contracts;
        using Microsoft.Extensions.DependencyInjection;
        using Tsikhanau.Monolith.Infrastructure.Dispatching;
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes.Infrastructure;

        internal sealed class NotesModuleFacade(IServiceScopeFactory serviceScopeFactory)
            : ModuleFacade<INotesModule>(serviceScopeFactory), INotesModule
        {
            public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                INotesCommand<TResult> command,
                CancellationToken cancellationToken)
                where TResult : notnull =>
                SendAsync(command, cancellationToken);

            public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
                INotesQuery<TResult> query,
                CancellationToken cancellationToken)
                where TResult : notnull =>
                QueryAsync(query, cancellationToken);
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
    public async Task First_command_adds_the_command_marker_and_rewrites_the_facade_with_its_method()
    {
        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var status = ReadStatus();
        var output = _output.ToString();
        var facadeContract = _app.Read(NotesFacadeContract);
        var facadeClass = _app.Read(NotesFacadeClass);
        var commandMarker = _app.Read(NotesCommandMarker);

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            "scaffold: done; the new test ArchiveNoteTests.Archive_note_succeeds_through_the_facade fails with "
            + "\"ArchiveNote in Notes is not implemented\" until the handler does its work; next, run ./gate.sh\n");

        status.ShouldBe(
            [
                $" M {NotesFacadeContract}",
                $" M {NotesFacadeClass}",
                $"?? {NotesCommandMarker}",
                $"?? {NotesProject}/Features/ArchiveNote/ArchiveNoteCommand.cs",
                $"?? {NotesProject}/Features/ArchiveNote/ArchiveNoteCommandHandler.cs",
                $"?? {NotesTests}/ArchiveNoteTests.cs",
            ],
            ignoreOrder: true);

        facadeContract.ShouldBe(FacadeContractWithCommands);
        facadeClass.ShouldBe(FacadeClassWithCommands);
        commandMarker.ShouldBe(
            """
            using Tsikhanau.Monolith.Application.Commands;

            namespace Acme.Shop.Modules.Notes;

            internal interface INotesCommand<TResult> : ICommand<TResult>
                where TResult : notnull;

            """);
    }

    [Fact]
    public async Task Second_command_writes_only_its_own_files()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");

        var exitCode = await RunAsync("feature Notes CreateNote --kind command --endpoint post");

        var status = ReadStatus();

        exitCode.ShouldBe(0, _error.ToString());
        status.ShouldBe(
            [
                $"?? {CreateNoteFolder}/CreateNoteCommand.cs",
                $"?? {CreateNoteFolder}/CreateNoteCommandHandler.cs",
                $"?? {CreateNoteFolder}/CreateNoteRequest.cs",
                $"?? {CreateNoteFolder}/CreateNoteEndpoint.cs",
                $"?? {NotesTests}/CreateNoteTests.cs",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Second_command_leaves_a_facade_changed_by_hand_alone()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");
        _app.Write(NotesFacadeClass, _app.Read(NotesFacadeClass) + "\n");
        _app.Commit();

        var exitCode = await RunAsync("feature Notes DeleteNote --kind command --endpoint delete");

        var changedFiles = ReadStatus()
            .Where(line => line.StartsWith(" M", StringComparison.Ordinal));

        exitCode.ShouldBe(0, _error.ToString());
        changedFiles.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--endpoint delete --kind command", new[] { "Command", "CommandHandler", "Endpoint" })]
    [InlineData("--kind command --endpoint DELETE", new[] { "Command", "CommandHandler", "Endpoint" })]
    [InlineData("--kind command --endpoint none", new[] { "Command", "CommandHandler" })]
    public async Task Options_come_in_any_order_and_the_method_in_any_letter_case(String options, String[] suffixes)
    {
        var exitCode = await RunAsync($"feature Tags DeleteTag {options}");

        var featureFolder = Path.Combine(_app.Root, TagsProject, "Features/DeleteTag");
        var featureFiles = Directory.Exists(featureFolder)
            ? Directory
                .EnumerateFiles(featureFolder)
                .Select(Path.GetFileName)
            : [];

        exitCode.ShouldBe(0, _error.ToString());
        featureFiles.ShouldBe(suffixes.Select(suffix => $"DeleteTag{suffix}.cs"), ignoreOrder: true);
    }

    [Theory]
    [InlineData(NotesFacadeContract)]
    [InlineData(NotesFacadeClass)]
    public async Task Facade_changed_by_hand_is_refused_before_anything_is_written(String path)
    {
        _app.Write(path, _app.Read(path) + "\n");
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: {path}{HandChangedFacade}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Facade_with_windows_line_endings_keeps_them()
    {
        _app.Write(NotesFacadeContract, ToWindowsLineEndings(_app.Read(NotesFacadeContract)));
        _app.Write(NotesFacadeClass, ToWindowsLineEndings(_app.Read(NotesFacadeClass)));
        _app.Commit();

        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var facadeContract = _app.Read(NotesFacadeContract);
        var facadeClass = _app.Read(NotesFacadeClass);

        exitCode.ShouldBe(0, _error.ToString());
        facadeContract.ShouldBe(ToWindowsLineEndings(FacadeContractWithCommands));
        facadeClass.ShouldBe(ToWindowsLineEndings(FacadeClassWithCommands));
    }

    [Theory]
    [InlineData(
        "feature Orders ArchiveOrder --kind command",
        "src/Modules has no module Orders (modules: Notes, Tags)")]
    [InlineData(
        "feature notes ArchiveNote --kind command",
        "src/Modules has no module notes (modules: Notes, Tags)")]
    [InlineData(
        "feature Notes archiveNote --kind command",
        "feature name 'archiveNote' must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData(
        "feature Notes ArchiveNoteCommand --kind command",
        "feature name 'ArchiveNoteCommand' must not end in Command: scaffold adds the suffix of each type itself")]
    [InlineData(
        "feature Notes ShowNote --kind command --endpoint get",
        "a command answers POST, PUT or DELETE, not GET: a GET only reads, "
        + "so give it a query (--kind query --endpoint get)")]
    [InlineData(
        "feature Notes CountNotes --kind query --endpoint post",
        "a query answers only GET, not POST: a change of state is a command "
        + "(--kind command --endpoint post, put or delete)")]
    [InlineData(
        "feature Tags GetTag --kind public-query --endpoint get",
        "a public query has no endpoint: neighbours ask ITagsQueryFacade for it; "
        + "for HTTP add a query of the module (--kind query --endpoint get)")]
    public async Task Unknown_module_bad_name_or_method_that_does_not_fit_the_kind_is_refused_before_any_write(
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
    public async Task Module_folder_without_its_project_is_refused()
    {
        _app.Write("src/Modules/Zones/readme.md", "zones");
        _app.Commit();

        var exitCode = await RunAsync("feature Zones ArchiveZone --kind command");

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: src/Modules/Zones holds no project "
            + "src/Modules/Zones/Acme.Shop.Modules.Zones/Acme.Shop.Modules.Zones.csproj\n");
    }

    [Fact]
    public async Task Feature_that_exists_is_refused_with_what_is_there()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command --endpoint delete");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: feature ArchiveNote of Notes already exists: {NotesProject}/Features/ArchiveNote, "
            + $"{NotesProject}/Features/ArchiveNote/ArchiveNoteCommand.cs, "
            + $"{NotesProject}/Features/ArchiveNote/ArchiveNoteCommandHandler.cs, "
            + $"{NotesTests}/ArchiveNoteTests.cs\n");

        after.ShouldBe(before);
    }

    [Theory]
    [InlineData($"{NotesProject}/Features/archiveNote/readme.md", $"{NotesProject}/Features/archiveNote")]
    [InlineData($"{NotesTests}/ArchiveNoteTests.cs", $"{NotesTests}/ArchiveNoteTests.cs")]
    [InlineData($"{NotesContracts}/Queries/archiveNote/readme.md", $"{NotesContracts}/Queries/archiveNote")]
    public async Task Feature_folder_in_another_letter_case_or_a_test_left_from_an_old_feature_is_refused(
        String leftOver,
        String reported)
    {
        _app.Write(leftOver, "namespace Old;\n");
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: feature ArchiveNote of Notes already exists: {reported}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Feature_name_that_makes_a_line_longer_than_120_characters_is_refused()
    {
        var name = $"Archive{new String('x', 73)}";

        var exitCode = await RunAsync($"feature Tags {name} --kind command");

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: feature name '{name}' makes {TagsProject}/Features/{name}/{name}Command.cs:3 "
            + "123 characters long, more than the 120 the build allows; choose a shorter name\n");
    }

    [Fact]
    public async Task App_without_an_integration_test_project_is_refused()
    {
        _app.Delete(SampleApp.IntegrationTestsProjectPath);
        _app.Commit();

        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: src/Tests/IntegrationTests must hold one *.IntegrationTests.csproj, "
            + "where scaffold puts the feature's test\n");
    }

    [Theory]
    [InlineData(1, NotesCommandMarker)]
    [InlineData(2, $"{CreateNoteFolder}/CreateNoteCommand.cs")]
    [InlineData(3, $"{CreateNoteFolder}/CreateNoteCommandHandler.cs")]
    [InlineData(4, $"{CreateNoteFolder}/CreateNoteRequest.cs")]
    [InlineData(5, $"{CreateNoteFolder}/CreateNoteEndpoint.cs")]
    [InlineData(6, $"{NotesTests}/CreateNoteTests.cs")]
    [InlineData(7, NotesFacadeContract)]
    [InlineData(8, NotesFacadeClass)]
    public async Task Failed_write_of_a_first_command_leaves_the_tree_as_it_was(Int32 failingWrite, String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            "feature Notes CreateNote --kind command --endpoint post");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData(1, $"{NotesProject}/Features/DeleteNote/DeleteNoteCommand.cs")]
    [InlineData(4, $"{NotesTests}/DeleteNoteTests.cs")]
    public async Task Failed_write_of_a_second_command_leaves_the_tree_as_it_was(Int32 failingWrite, String failingPath)
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            "feature Notes DeleteNote --kind command --endpoint delete");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task First_query_of_a_module_with_commands_adds_the_query_markers_and_the_query_facade_to_the_facade()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");

        var exitCode = await RunAsync("feature Notes CountNotes --kind query");

        var status = ReadStatus();
        var output = _output.ToString();
        var facadeContract = _app.Read(NotesFacadeContract);
        var facadeClass = _app.Read(NotesFacadeClass);
        var queryMarker = _app.Read(NotesQueryMarker);
        var queryFacade = _app.Read(NotesQueryFacade);

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            "scaffold: done; the new test CountNotesTests.Count_notes_succeeds_through_the_facade fails with "
            + "\"CountNotes in Notes is not implemented\" until the handler does its work; next, run ./gate.sh\n");

        status.ShouldBe(
            [
                $" M {NotesFacadeContract}",
                $" M {NotesFacadeClass}",
                $"?? {NotesQueryMarker}",
                $"?? {NotesQueryFacade}",
                $"?? {NotesProject}/Features/CountNotes/CountNotesQuery.cs",
                $"?? {NotesProject}/Features/CountNotes/CountNotesDto.cs",
                $"?? {NotesProject}/Features/CountNotes/CountNotesQueryHandler.cs",
                $"?? {NotesTests}/CountNotesTests.cs",
            ],
            ignoreOrder: true);

        facadeContract.ShouldBe(FacadeContractWithCommandsAndQueries);
        facadeClass.ShouldBe(FacadeClassWithCommandsAndQueries);
        queryMarker.ShouldBe(
            """
            using Tsikhanau.Monolith.Contracts.Queries;

            namespace Acme.Shop.Modules.Notes.Contracts;

            public interface INotesQuery<TResult> : IQuery<TResult>
                where TResult : notnull;

            """);

        queryFacade.ShouldBe(
            """
            using Tsikhanau.Monolith.Contracts.Modules;
            using Tsikhanau.Railway;

            namespace Acme.Shop.Modules.Notes.Contracts;

            public interface INotesQueryFacade : IModuleQueryFacade
            {
                Task<Result<TResult>> ExecuteQueryAsync<TResult>(INotesQuery<TResult> query, CancellationToken cancellationToken)
                    where TResult : notnull;
            }

            """);
    }

    [Fact]
    public async Task First_public_query_of_a_module_without_commands_gives_the_facade_only_the_query_method()
    {
        var exitCode = await RunAsync("feature Tags GetTag --kind public-query");

        var status = ReadStatus();
        var output = _output.ToString();
        var facadeContract = _app.Read(TagsFacadeContract);
        var facadeClass = _app.Read(TagsFacadeClass);

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            "scaffold: done; the new test GetTagTests.Get_tag_succeeds_through_the_query_facade fails with "
            + "\"GetTag in Tags is not implemented\" until the handler does its work; next, run ./gate.sh\n");

        status.ShouldBe(
            [
                $" M {TagsFacadeContract}",
                $" M {TagsFacadeClass}",
                $"?? {TagsContracts}/ITagsQuery.cs",
                $"?? {TagsContracts}/ITagsQueryFacade.cs",
                $"?? {TagsContracts}/Queries/GetTag/GetTagQuery.cs",
                $"?? {TagsContracts}/Queries/GetTag/TagDto.cs",
                $"?? {TagsProject}/Features/GetTag/GetTagQueryHandler.cs",
                $"?? {TagsTests}/GetTagTests.cs",
            ],
            ignoreOrder: true);

        facadeContract.ShouldBe(
            """
            using Acme.Shop.Modules.Tags.Contracts;
            using Tsikhanau.Monolith.Application.Modules;

            namespace Acme.Shop.Modules.Tags;

            internal interface ITagsModule : ITagsQueryFacade, IModuleFacade;

            """);

        facadeClass.ShouldBe(
            """
            using Acme.Shop.Modules.Tags.Contracts;
            using Microsoft.Extensions.DependencyInjection;
            using Tsikhanau.Monolith.Infrastructure.Dispatching;
            using Tsikhanau.Railway;

            namespace Acme.Shop.Modules.Tags.Infrastructure;

            internal sealed class TagsModuleFacade(IServiceScopeFactory serviceScopeFactory)
                : ModuleFacade<ITagsModule>(serviceScopeFactory), ITagsModule
            {
                public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
                    ITagsQuery<TResult> query,
                    CancellationToken cancellationToken)
                    where TResult : notnull =>
                    QueryAsync(query, cancellationToken);
            }

            """);
    }

    [Fact]
    public async Task First_command_of_a_module_with_queries_adds_the_command_method_next_to_the_query_method()
    {
        await RunAndCommitAsync("feature Tags GetTag --kind public-query");

        var exitCode = await RunAsync("feature Tags ArchiveTag --kind command");

        var facadeContract = _app.Read(TagsFacadeContract);
        var facadeClass = _app.Read(TagsFacadeClass);

        exitCode.ShouldBe(0, _error.ToString());
        facadeContract.ShouldBe(RenameModule(FacadeContractWithCommandsAndQueries, "Tags"));
        facadeClass.ShouldBe(RenameModule(FacadeClassWithCommandsAndQueries, "Tags"));
    }

    [Fact]
    public async Task Second_query_writes_only_its_own_files()
    {
        await RunAndCommitAsync("feature Notes CountNotes --kind query");

        var exitCode = await RunAsync("feature Notes GetNoteSummary --kind query --endpoint get");

        var status = ReadStatus();

        exitCode.ShouldBe(0, _error.ToString());
        status.ShouldBe(
            [
                $"?? {GetNoteSummaryFolder}/GetNoteSummaryQuery.cs",
                $"?? {GetNoteSummaryFolder}/NoteSummaryDto.cs",
                $"?? {GetNoteSummaryFolder}/GetNoteSummaryQueryHandler.cs",
                $"?? {GetNoteSummaryFolder}/GetNoteSummaryEndpoint.cs",
                $"?? {NotesTests}/GetNoteSummaryTests.cs",
            ],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData("feature Notes CountNotes --kind query", NotesQueryMarker)]
    [InlineData("feature Notes CountNotes --kind query", NotesQueryFacade)]
    [InlineData("feature Notes ArchiveNote --kind command", NotesQueryMarker)]
    public async Task Query_marker_without_the_query_facade_or_the_other_way_round_is_refused(
        String commandLine,
        String loneFile)
    {
        _app.Write(loneFile, "namespace Acme.Shop.Modules.Notes.Contracts;\n");
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(commandLine);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: {NotesContracts} must hold both INotesQuery.cs and INotesQueryFacade.cs or neither of them\n");

        after.ShouldBe(before);
    }

    [Theory]
    [InlineData(NotesFacadeContract)]
    [InlineData(NotesFacadeClass)]
    public async Task Facade_changed_by_hand_is_refused_before_a_first_query_writes_anything(String path)
    {
        _app.Write(path, _app.Read(path) + "\n");
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes CountNotes --kind query");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: {path}{HandChangedFacadeForQueries}\n");
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("NoteSummary", "GetNoteSummary")]
    [InlineData("GetNoteSummary", "NoteSummary")]
    public async Task Query_whose_route_the_module_maps_already_is_refused_before_any_write(String first, String second)
    {
        await RunAndCommitAsync($"feature Notes {first} --kind query --endpoint get");
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync($"feature Notes {second} --kind query --endpoint get");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: feature {second} of Notes would answer GET /notes/note-summary, which "
            + $"{NotesProject}/Features/{first}/{first}Endpoint.cs:12 maps already; choose another feature name\n");

        after.ShouldBe(before);
    }

    [Fact]
    public async Task Public_query_that_exists_is_refused_with_both_its_folders_and_its_files()
    {
        await RunAndCommitAsync("feature Tags GetTag --kind public-query");
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Tags GetTag --kind public-query");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $"scaffold: feature GetTag of Tags already exists: {TagsProject}/Features/GetTag, "
            + $"{TagsContracts}/Queries/GetTag, "
            + $"{TagsContracts}/Queries/GetTag/GetTagQuery.cs, "
            + $"{TagsContracts}/Queries/GetTag/TagDto.cs, "
            + $"{TagsProject}/Features/GetTag/GetTagQueryHandler.cs, "
            + $"{TagsTests}/GetTagTests.cs\n");

        after.ShouldBe(before);
    }

    [Fact]
    public async Task Module_folder_without_its_contracts_project_is_refused()
    {
        _app.Delete($"{NotesContracts}/Acme.Shop.Modules.Notes.Contracts.csproj");
        _app.Commit();

        var exitCode = await RunAsync("feature Notes CountNotes --kind query");

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: src/Modules/Notes holds no project "
            + $"{NotesContracts}/Acme.Shop.Modules.Notes.Contracts.csproj\n");
    }

    [Theory]
    [InlineData(1, NotesQueryMarker)]
    [InlineData(2, NotesQueryFacade)]
    [InlineData(3, $"{GetNoteSummaryFolder}/GetNoteSummaryQuery.cs")]
    [InlineData(4, $"{GetNoteSummaryFolder}/NoteSummaryDto.cs")]
    [InlineData(5, $"{GetNoteSummaryFolder}/GetNoteSummaryQueryHandler.cs")]
    [InlineData(6, $"{GetNoteSummaryFolder}/GetNoteSummaryEndpoint.cs")]
    [InlineData(7, $"{NotesTests}/GetNoteSummaryTests.cs")]
    [InlineData(8, NotesFacadeContract)]
    [InlineData(9, NotesFacadeClass)]
    public async Task Failed_write_of_a_first_query_leaves_the_tree_as_it_was(Int32 failingWrite, String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            "feature Notes GetNoteSummary --kind query --endpoint get");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData(3, $"{TagsContracts}/Queries/GetTag/GetTagQuery.cs")]
    [InlineData(5, $"{TagsProject}/Features/GetTag/GetTagQueryHandler.cs")]
    [InlineData(8, TagsFacadeClass)]
    public async Task Failed_write_of_a_first_public_query_leaves_the_tree_as_it_was(
        Int32 failingWrite,
        String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            "feature Tags GetTag --kind public-query");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

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

    private static String ToWindowsLineEndings(String text) => text.Replace("\n", "\r\n", StringComparison.Ordinal);

    private static String RenameModule(String text, String moduleName) =>
        text.Replace("Notes", moduleName, StringComparison.Ordinal);
}
