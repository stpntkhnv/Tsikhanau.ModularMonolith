using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class PersistenceMutations(SourceTree sourceTree)
{
    private const String NoteConfiguration = $"{ScratchPaths.Notes}/Infrastructure/Persistence/NoteConfiguration.cs";

    private const String NotesProject = $"{ScratchPaths.Notes}/ModularMonolith.Modules.Notes.csproj";

    private const String MigrationsFolder = "Infrastructure/Migrations";

    private const String MoveNotesPattern = "*_MoveNotes.cs";

    private const String AddMember = "    void Add(Note note);";

    private const String GetMember = "    public async Task<Result<Note>> GetAsync";

    public Result<Unit> RepositoryReturnsNull() =>
        sourceTree
            .Replace(
                ScratchPaths.NoteRepositoryInterface,
                AddMember,
                $"{AddMember}\n\n    Task<Note?> FindAsync(NoteId noteId, CancellationToken cancellationToken);")
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.NoteRepository,
                GetMember,
                $$"""
                    public async Task<Note?> FindAsync(NoteId noteId, CancellationToken cancellationToken) =>
                        await notesDbContext
                            .Set<Note>()
                            .FindAsync([noteId], cancellationToken);

                {{GetMember}}
                """));

    public Result<Unit> CancellationTokenNotPassed() =>
        sourceTree
            .Replace(ScratchPaths.NoteRepository, ".FindAsync([noteId], cancellationToken);", ".FindAsync(noteId);")
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.GetNoteQueryHandler,
                "new CommandDefinition(Sql, new { query.NoteId }, cancellationToken: cancellationToken)",
                "new CommandDefinition(Sql, new { query.NoteId })"))
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.GetNoteQueryHandler,
                "await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);",
                "await sqlConnectionFactory.OpenConnectionAsync(CancellationToken.None);"));

    public Result<Unit> MigrationInWrongSchema() => MoveNotesTo("tags");

    public Result<Unit> MigrationInPublicSchema() => MoveNotesTo("public");

    private Result<Unit> MoveNotesTo(String schema) =>
        sourceTree
            .Replace(NoteConfiguration, "builder.ToTable(\"notes\");", $"builder.ToTable(\"notes\", \"{schema}\");")
            .Bind(_ => BuildNotes())
            .Bind(_ => AddMoveNotesMigration())
            .Bind(_ => ReadMoveNotesMigration())
            .Ensure(migration => MovesTo(migration, schema), NotMovedTo(schema))
            .Map(_ => Unit.Value);

    private Result<Unit> BuildNotes() =>
        new DotnetCommand(sourceTree.Root).Run("build", NotesProject, "-nologo", "-v", "q");

    private Result<Unit> AddMoveNotesMigration() =>
        new DotnetCommand(Path.Combine(sourceTree.Root, ScratchPaths.Notes))
            .Run("ef", "migrations", "add", "MoveNotes", "--output-dir", MigrationsFolder, "--no-build");

    private Result<String> ReadMoveNotesMigration()
    {
        var migrations = sourceTree.FindFiles($"{ScratchPaths.Notes}/{MigrationsFolder}", MoveNotesPattern);

        return migrations.Count == 1
            ? sourceTree.Read(migrations[0])
            : Error.Failure("harness.migration", $"expected one {MoveNotesPattern}, found {migrations.Count}");
    }

    private static Boolean MovesTo(String migration, String schema) =>
        migration.Contains($"newSchema: \"{schema}\"", StringComparison.Ordinal);

    private static Error NotMovedTo(String schema) =>
        Error.Failure("harness.migration", $"the MoveNotes migration does not move the notes table to {schema}");
}
