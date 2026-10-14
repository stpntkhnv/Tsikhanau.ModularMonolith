using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class HandlerResultMutations(SourceTree sourceTree)
{
    public Result<Unit> HandlerThrowsForExpectedFailure() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    if (String.IsNullOrWhiteSpace(command.Title))
                    {
                        throw new ArgumentException("Title is required", nameof(command));
                    }

            """);

    public Result<Unit> ResultIgnored() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    NoteTitle.Create(command.Title, "title");

            """);

    public Result<Unit> ResultDiscardedExplicitly() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    _ = NoteTitle.Create(command.Title, "title");

            """);

    public Result<Unit> TaskResultNotAwaited() =>
        sourceTree.Write(
            ScratchPaths.CreateNoteHandler,
            """
            using ModularMonolith.Modules.Notes.Domain.Notes;
            using Tsikhanau.Monolith.Application.Commands;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Notes.Features.CreateNote;

            internal sealed class CreateNoteCommandHandler(INoteRepository noteRepository, TimeProvider timeProvider)
                : ICommandHandler<CreateNoteCommand, Guid>
            {
                public async Task<Result<Guid>> Handle(CreateNoteCommand command, CancellationToken cancellationToken)
                {
                    noteRepository.GetAsync(NoteId.From(command.NoteId), cancellationToken);
                    await Task.Yield();

                    return CreateNote(command)
                        .Tap(noteRepository.Add)
                        .Map(note => note.Id.Value);
                }

                private Result<Note> CreateNote(CreateNoteCommand command) =>
                    Result
                        .Collect(NoteId.Parse(command.NoteId, "noteId"))
                        .And(NoteTitle.Create(command.Title, "title"))
                        .Map((noteId, title) => Note.Create(noteId, title, timeProvider.GetUtcNow()));
            }

            """);

    public Result<Unit> ValueWithoutCheck() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    var title = NoteTitle.Create(command.Title, "title").Value;

                    _ = title;

            """);

    public Result<Unit> ValueAfterWrongCheck() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    var parsed = NoteTitle.Create(command.Title, "title");
                    var title = parsed.IsFailure ? parsed.Value : default;

                    _ = title;

            """);

    public Result<Unit> StaticMutableState() =>
        sourceTree.WriteCreateNoteHandler(
            "        Created++;\n",
            members: """
                private static Int32 Created;


            """);

    public Result<Unit> AsyncVoid() =>
        sourceTree.WriteCreateNoteHandler(
            "        Audit(cancellationToken);\n",
            members: """
                private static async void Audit(CancellationToken cancellationToken) =>
                    await Task.Delay(1, cancellationToken);


            """);

    public Result<Unit> TaskWaitedSynchronously() =>
        sourceTree.WriteCreateNoteHandler(
            """
                    var loading = noteRepository.GetAsync(NoteId.From(command.NoteId), cancellationToken);
                    loading.Wait();

            """);
}
