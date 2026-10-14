using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class MessageMutations(SourceTree sourceTree)
{
    public Result<Unit> MutableCommand() =>
        sourceTree.Write(
            $"{ScratchPaths.Notes}/Features/CreateNote/CreateNoteCommand.cs",
            """
            namespace ModularMonolith.Modules.Notes.Features.CreateNote;

            internal sealed record CreateNoteCommand(Guid NoteId, String Title) : INotesCommand<Guid>
            {
                public String Title { get; set; } = Title;
            }

            """);

    public Result<Unit> IntegrationEventWithoutName() =>
        sourceTree.Replace(ScratchPaths.NoteCreatedEvent, "[IntegrationEventName(\"notes.note-created\")]\n", "");

    public Result<Unit> IntegrationEventCarriesDomainType() =>
        sourceTree.Replace(
            ScratchPaths.NoteCreatedEvent,
            "(DateTimeOffset OccurredOn, Guid NoteId, String Title)",
            """
            (
                DateTimeOffset OccurredOn,
                Guid NoteId,
                ModularMonolith.Modules.Notes.Domain.Notes.NoteTitle Title)
            """);

    public Result<Unit> IntegrationEventCarriesBuildingBlockDomainType() =>
        sourceTree
            .Replace(
                ScratchPaths.NotesContractsProject,
                ScratchPaths.ContractsCoreReference,
                $"{ScratchPaths.ContractsCoreReference}\n    {ScratchPaths.DomainCoreReference}")
            .Bind(_ => sourceTree.Write(
                ScratchPaths.NoteCreatedEvent,
                """
                using Tsikhanau.Monolith.Contracts.IntegrationEvents;
                using Tsikhanau.Monolith.Domain.Events;

                namespace ModularMonolith.Modules.Notes.Contracts.IntegrationEvents;

                [IntegrationEventName("notes.note-created")]
                public sealed record NoteCreatedIntegrationEvent(
                    DateTimeOffset OccurredOn,
                    Guid NoteId,
                    String Title,
                    DomainEvent? Cause = null)
                    : IntegrationEvent(OccurredOn);

                """));

    public Result<Unit> QueryResultNullable() =>
        sourceTree.Replace(
            $"{ScratchPaths.NotesContracts}/Queries/GetNote/GetNoteQuery.cs",
            "INotesQuery<NoteDto>",
            "INotesQuery<NoteDto?>");
}
