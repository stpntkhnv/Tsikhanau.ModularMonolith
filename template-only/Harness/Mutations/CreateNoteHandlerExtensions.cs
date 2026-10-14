using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal static class CreateNoteHandlerExtensions
{
    private const String Dependencies = "INoteRepository noteRepository, TimeProvider timeProvider";

    private const String CreatedOn = "timeProvider.GetUtcNow()";

    private const String HandleSignature =
        "    public Task<Result<Guid>> Handle(CreateNoteCommand command, CancellationToken cancellationToken)";

    extension(SourceTree sourceTree)
    {
        public Result<Unit> WriteCreateNoteHandler(
            String bodyBefore,
            String extraUsings = "",
            String dependencies = Dependencies,
            String createdOn = CreatedOn,
            String members = "") =>
            sourceTree.Write(
                ScratchPaths.CreateNoteHandler,
                $$"""
                using ModularMonolith.Modules.Notes.Domain.Notes;
                using Tsikhanau.Monolith.Application.Commands;
                using Tsikhanau.Railway;
                {{extraUsings}}
                namespace ModularMonolith.Modules.Notes.Features.CreateNote;

                internal sealed class CreateNoteCommandHandler({{dependencies}})
                    : ICommandHandler<CreateNoteCommand, Guid>
                {
                {{members}}{{HandleSignature}}
                    {
                {{bodyBefore}}
                        return Task.FromResult(
                            CreateNote(command)
                                .Tap(noteRepository.Add)
                                .Map(note => note.Id.Value));
                    }

                    private Result<Note> CreateNote(CreateNoteCommand command) =>
                        Result
                            .Collect(NoteId.Parse(command.NoteId, "noteId"))
                            .And(NoteTitle.Create(command.Title, "title"))
                            .Map((noteId, title) => Note.Create(noteId, title, {{createdOn}}));
                }

                """);

        public Result<Unit> WriteCreateNoteHandlerTaking(
            String thirdDependency,
            String extraUsing,
            String bodyBefore) =>
            sourceTree.WriteCreateNoteHandler(
                bodyBefore,
                extraUsing,
                $"\n    INoteRepository noteRepository,\n    TimeProvider timeProvider,\n    {thirdDependency}");
    }
}
