using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class SqlMutations(SourceTree sourceTree)
{
    private const String SelectLine = "        SELECT id AS \"Id\", title AS \"Title\"";

    public Result<Unit> QueryHandlerWrites() =>
        sourceTree.Replace(
            ScratchPaths.GetNoteQueryHandler,
            SelectLine,
            $"        UPDATE notes.notes SET title = title WHERE id = @NoteId;\n{SelectLine}");

    public Result<Unit> SqlNamesOtherModuleSchema() =>
        sourceTree.Replace(
            ScratchPaths.GetNoteQueryHandler,
            "FROM notes.notes",
            "FROM notes.notes JOIN tags.inbox_messages ON true");

    public Result<Unit> OtherModuleSqlNamesNotesSchema() =>
        sourceTree.Write(
            ScratchPaths.TagsNoteCreatedHandler,
            """
            using Dapper;
            using ModularMonolith.Modules.Notes.Contracts.IntegrationEvents;
            using Tsikhanau.Monolith.Application.Events;
            using Tsikhanau.Monolith.Application.Queries;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Tags.Features.NoteCreated;

            internal sealed class NoteCreatedIntegrationEventHandler(ISqlConnectionFactory<ITagsModule> sqlConnectionFactory)
                : IIntegrationEventHandler<NoteCreatedIntegrationEvent>
            {
                public async Task<Result<Unit>> Handle(
                    NoteCreatedIntegrationEvent integrationEvent,
                    CancellationToken cancellationToken)
                {
                    await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
                    var title = await connection.ExecuteScalarAsync<String>(
                        new CommandDefinition(
                            "SELECT title FROM notes.notes WHERE id = @NoteId",
                            new { integrationEvent.NoteId },
                            cancellationToken: cancellationToken));

                    return title is null ? Error.NotFound("Tags.NoNote", "No note") : Unit.Value;
                }
            }

            """);

    public Result<Unit> SqlBuiltFromInput() =>
        sourceTree
            .Replace(
                ScratchPaths.GetNoteQueryHandler,
                "    private const String Sql = ",
                "    private const String UnusedSql = ")
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.GetNoteQueryHandler,
                "new CommandDefinition(Sql, new { query.NoteId }, cancellationToken: cancellationToken)",
                """
                $"SELECT id AS \"Id\", title AS \"Title\" FROM notes.notes WHERE id = '{query.NoteId}'"
                """));
}
