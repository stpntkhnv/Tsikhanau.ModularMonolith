namespace Tsikhanau.Monolith.Harness.Mutations;

internal static class ScratchPaths
{
    public const String Notes = "src/Modules/Notes/ModularMonolith.Modules.Notes";

    public const String NotesContracts = "src/Modules/Notes/ModularMonolith.Modules.Notes.Contracts";

    public const String Tags = "src/Modules/Tags/ModularMonolith.Modules.Tags";

    public const String CreateNoteHandler = $"{Notes}/Features/CreateNote/CreateNoteCommandHandler.cs";

    public const String GetNoteEndpoint = $"{Notes}/Features/GetNote/GetNoteEndpoint.cs";

    public const String GetNoteQueryHandler = $"{Notes}/Features/GetNote/GetNoteQueryHandler.cs";

    public const String Note = $"{Notes}/Domain/Notes/Note.cs";

    public const String NoteRepositoryInterface = $"{Notes}/Domain/Notes/INoteRepository.cs";

    public const String NoteRepository = $"{Notes}/Infrastructure/Persistence/NoteRepository.cs";

    public const String NoteCreatedEvent = $"{NotesContracts}/IntegrationEvents/NoteCreatedIntegrationEvent.cs";

    public const String NotesContractsProject = $"{NotesContracts}/ModularMonolith.Modules.Notes.Contracts.csproj";

    public const String TagsNoteCreatedHandler = $"{Tags}/Features/NoteCreated/NoteCreatedIntegrationEventHandler.cs";

    public const String ContractsCoreReference =
        """<ProjectReference Include="..\..\..\Core\Contracts\Tsikhanau.Monolith.Contracts.csproj" />""";

    public const String DomainCoreReference =
        """<ProjectReference Include="..\..\..\Core\Domain\Tsikhanau.Monolith.Domain.csproj" />""";
}
