using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class EventHandlerMutations(SourceTree sourceTree)
{
    public Result<Unit> TaskResultBlocks() =>
        sourceTree.Write(
            ScratchPaths.TagsNoteCreatedHandler,
            """
            using ModularMonolith.Modules.Notes.Contracts;
            using ModularMonolith.Modules.Notes.Contracts.IntegrationEvents;
            using ModularMonolith.Modules.Notes.Contracts.Queries.GetNote;
            using Tsikhanau.Monolith.Application.Events;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Tags.Features.NoteCreated;

            internal sealed class NoteCreatedIntegrationEventHandler(INotesQueryFacade notesQueryFacade)
                : IIntegrationEventHandler<NoteCreatedIntegrationEvent>
            {
                public Task<Result<Unit>> Handle(
                    NoteCreatedIntegrationEvent integrationEvent,
                    CancellationToken cancellationToken)
                {
                    var note = notesQueryFacade
                        .ExecuteQueryAsync(new GetNoteQuery(integrationEvent.NoteId), cancellationToken)
                        .Result;

                    return Task.FromResult(note.Map(_ => Unit.Value));
                }
            }

            """);

    public Result<Unit> ExceptionSwallowed() =>
        sourceTree.Write(
            ScratchPaths.TagsNoteCreatedHandler,
            """
            using ModularMonolith.Modules.Notes.Contracts;
            using ModularMonolith.Modules.Notes.Contracts.IntegrationEvents;
            using ModularMonolith.Modules.Notes.Contracts.Queries.GetNote;
            using Tsikhanau.Monolith.Application.Events;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Tags.Features.NoteCreated;

            internal sealed class NoteCreatedIntegrationEventHandler(INotesQueryFacade notesQueryFacade)
                : IIntegrationEventHandler<NoteCreatedIntegrationEvent>
            {
                public async Task<Result<Unit>> Handle(
                    NoteCreatedIntegrationEvent integrationEvent,
                    CancellationToken cancellationToken)
                {
                    try
                    {
                        var note = await notesQueryFacade.ExecuteQueryAsync(
                            new GetNoteQuery(integrationEvent.NoteId),
                            cancellationToken);

                        return note.Map(_ => Unit.Value);
                    }
                    catch (Exception)
                    {
                        return Unit.Value;
                    }
                }
            }

            """);
}
