using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class HandlerDependencyMutations(SourceTree sourceTree)
{
    public Result<Unit> HandlerUsesDbContext() =>
        sourceTree.Write(
            ScratchPaths.CreateNoteHandler,
            """
            using ModularMonolith.Modules.Notes.Domain.Notes;
            using ModularMonolith.Modules.Notes.Infrastructure.Persistence;
            using Tsikhanau.Monolith.Application.Commands;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Notes.Features.CreateNote;

            internal sealed class CreateNoteCommandHandler(NotesDbContext notesDbContext, TimeProvider timeProvider)
                : ICommandHandler<CreateNoteCommand, Guid>
            {
                public Task<Result<Guid>> Handle(CreateNoteCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(
                        CreateNote(command)
                            .Tap(note => notesDbContext.Add(note))
                            .Map(note => note.Id.Value));

                private Result<Note> CreateNote(CreateNoteCommand command) =>
                    Result
                        .Collect(NoteId.Parse(command.NoteId, "noteId"))
                        .And(NoteTitle.Create(command.Title, "title"))
                        .Map((noteId, title) => Note.Create(noteId, title, timeProvider.GetUtcNow()));
            }

            """);

    public Result<Unit> HandlerTakesEfDbContextBase() =>
        sourceTree.WriteCreateNoteHandlerTaking(
            "DbContext dbContext",
            "using Microsoft.EntityFrameworkCore;\n",
            "        dbContext.ChangeTracker.Clear();\n");

    public Result<Unit> HandlerTakesNpgsqlDataSource() =>
        sourceTree.WriteCreateNoteHandlerTaking(
            "NpgsqlDataSource npgsqlDataSource",
            "using Npgsql;\n",
            "        using var npgsqlConnection = npgsqlDataSource.CreateConnection();\n");

    public Result<Unit> HandlerTakesHttpContext() =>
        sourceTree.WriteCreateNoteHandlerTaking(
            "IHttpContextAccessor httpContextAccessor",
            "using Microsoft.AspNetCore.Http;\n",
            """
                    var user = httpContextAccessor.HttpContext?.User;

                    _ = user;

            """);

    public Result<Unit> HandlerReadsConfiguration() =>
        sourceTree.WriteCreateNoteHandlerTaking(
            "IConfiguration configuration",
            "using Microsoft.Extensions.Configuration;\n",
            """
                    var limit = configuration["Notes:Max"];

                    _ = limit;

            """);

    public Result<Unit> HandlerNotSealed() =>
        sourceTree.Replace(
            ScratchPaths.CreateNoteHandler,
            "internal sealed class CreateNoteCommandHandler",
            "internal class CreateNoteCommandHandler");

    public Result<Unit> HandlerReadsTheClock() =>
        sourceTree.WriteCreateNoteHandler(
            "",
            dependencies: "INoteRepository noteRepository",
            createdOn: "DateTimeOffset.UtcNow");

    public Result<Unit> HandlerCreatesRandomId() =>
        sourceTree.Replace(
            ScratchPaths.CreateNoteHandler,
            """.Collect(NoteId.Parse(command.NoteId, "noteId"))""",
            """.Collect(NoteId.Parse(Guid.NewGuid(), "noteId"))""");
}
