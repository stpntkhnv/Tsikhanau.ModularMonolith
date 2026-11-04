using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Tests.Fakes;
using Tsikhanau.Monolith.Scaffold.Tests.Samples;

namespace Tsikhanau.Monolith.Scaffold.Tests.Apps;

public sealed class RailwayRootNamespaceTests : IAsyncLifetime
{
    private const String RootNamespace = "Tsikhanau.Railway";

    private const String RailwayUsing = "using Tsikhanau.Railway;\n";

    private const String NotesProject = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String NotesContracts = "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String NotesFacadeContract = $"{NotesProject}/INotesModule.cs";

    private const String NotesFacadeClass = $"{NotesProject}/Infrastructure/NotesModuleFacade.cs";

    private const String NotesQueryFacade = $"{NotesContracts}/INotesQueryFacade.cs";

    private const String FacadeContractWithCommands =
        """
        using Tsikhanau.Monolith.Application.Modules;
        using Tsikhanau.Railway;

        namespace Tsikhanau.Railway.Modules.Notes;

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

        namespace Tsikhanau.Railway.Modules.Notes.Infrastructure;

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
        using Tsikhanau.Monolith.Application.Modules;
        using Tsikhanau.Railway;
        using Tsikhanau.Railway.Modules.Notes.Contracts;

        namespace Tsikhanau.Railway.Modules.Notes;

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
        using Microsoft.Extensions.DependencyInjection;
        using Tsikhanau.Monolith.Infrastructure.Dispatching;
        using Tsikhanau.Railway;
        using Tsikhanau.Railway.Modules.Notes.Contracts;

        namespace Tsikhanau.Railway.Modules.Notes.Infrastructure;

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
        _app.Write(SampleApp.ProgramPath, SampleModules.RenameApp(FrozenPrograms.Core102, RootNamespace));
        _app.Commit();
        await RunAndCommitAsync("module Notes");
    }

    public ValueTask DisposeAsync()
    {
        _output.Dispose();
        _error.Dispose();
        _app.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Facade_scaffold_wrote_before_takes_a_first_query_and_keeps_the_railway_using()
    {
        await RunAndCommitAsync("feature Notes ArchiveNote --kind command");
        _app.Write(NotesFacadeContract, FacadeContractWithCommands);
        _app.Write(NotesFacadeClass, FacadeClassWithCommands);
        _app.Commit();

        var exitCode = await RunAsync("feature Notes CountNotes --kind query");

        var facadeContract = _app.Read(NotesFacadeContract);
        var facadeClass = _app.Read(NotesFacadeClass);
        var queryFacade = _app.Read(NotesQueryFacade);

        exitCode.ShouldBe(0, _error.ToString());
        facadeContract.ShouldBe(FacadeContractWithCommandsAndQueries);
        facadeClass.ShouldBe(FacadeClassWithCommandsAndQueries);
        queryFacade.ShouldContain(RailwayUsing, Case.Sensitive);
    }

    [Fact]
    public async Task Feature_test_keeps_the_railway_using_that_the_handler_in_an_enclosed_namespace_drops()
    {
        var exitCode = await RunAsync("feature Notes ArchiveNote --kind command");

        var test = _app.Read($"{NotesTests}/ArchiveNoteTests.cs");
        var handler = _app.Read($"{NotesProject}/Features/ArchiveNote/ArchiveNoteCommandHandler.cs");

        exitCode.ShouldBe(0, _error.ToString());
        test.ShouldContain(RailwayUsing, Case.Sensitive);
        handler.ShouldNotContain(RailwayUsing, Case.Sensitive);
    }

    private async Task RunAndCommitAsync(String commandLine)
    {
        var exitCode = await RunAsync(commandLine);

        exitCode.ShouldBe(0, _error.ToString());
        _app.Commit();
    }

    private Task<Int32> RunAsync(String commandLine) =>
        new ScaffoldTool(_app.FileSystem, FakeProcessRunner.Succeeding(), _output, _error)
            .RunAsync(commandLine.Split(' '), CancellationToken);
}
