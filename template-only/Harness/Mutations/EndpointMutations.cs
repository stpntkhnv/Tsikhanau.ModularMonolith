using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class EndpointMutations(SourceTree sourceTree)
{
    private const String CreateNoteEndpoint = $"{ScratchPaths.Notes}/Features/CreateNote/CreateNoteEndpoint.cs";

    public Result<Unit> EndpointUsesRepository() =>
        WriteGetNoteEndpoint(
            "INoteRepository noteRepository",
            """
                    var foundNote = await noteRepository.GetAsync(NoteId.From(noteId), cancellationToken);

                    return foundNote
                        .Map(note => note.Title.Value)
                        .ToHttpResult();
            """,
            """
            using ModularMonolith.Modules.Notes.Domain.Notes;
            using Tsikhanau.Railway;

            """);

    public Result<Unit> EndpointUsesDbContext() =>
        WriteGetNoteEndpoint(
            "NotesDbContext notesDbContext",
            """
                    Optional<Note> foundNote = await notesDbContext
                        .Set<Note>()
                        .FindAsync([NoteId.From(noteId)], cancellationToken);

                    return foundNote
                        .ToResult(NoteErrors.NotFound)
                        .Map(note => note.Title.Value)
                        .ToHttpResult();
            """,
            """
            using ModularMonolith.Modules.Notes.Domain.Notes;
            using ModularMonolith.Modules.Notes.Infrastructure.Persistence;
            using Tsikhanau.Railway;

            """);

    public Result<Unit> EndpointRunsRawSql() =>
        WriteGetNoteEndpoint(
            "ISqlConnectionFactory<INotesModule> sqlConnectionFactory",
            """
                    await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT title FROM notes.notes WHERE id = '{noteId}'";
                    Optional<Object> foundTitle = await command.ExecuteScalarAsync(cancellationToken);

                    return foundTitle
                        .ToResult(NoteErrors.NotFound)
                        .ToHttpResult();
            """,
            """
            using ModularMonolith.Modules.Notes.Domain.Notes;
            using Tsikhanau.Monolith.Application.Queries;
            using Tsikhanau.Railway;

            """);

    public Result<Unit> EndpointHoldsLogic() =>
        sourceTree.Replace(
            CreateNoteEndpoint,
            """
                private static Task<IResult> CreateAsync(
                    CreateNoteRequest request,
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteCommandAsync(new CreateNoteCommand(request.NoteId, request.Title), cancellationToken)
                        .ToHttpResultAsync();
            """,
            """
                private static async Task<IResult> CreateAsync(
                    CreateNoteRequest request,
                    INotesModule notesModule,
                    CancellationToken cancellationToken)
                {
                    if (request.Title.Length > 50 || request.Title.Contains("forbidden", StringComparison.Ordinal))
                    {
                        return Results.BadRequest("Title is not allowed");
                    }

                    return await notesModule
                        .ExecuteCommandAsync(new CreateNoteCommand(request.NoteId, request.Title), cancellationToken)
                        .ToHttpResultAsync();
                }
            """);

    private Result<Unit> WriteGetNoteEndpoint(String parameters, String body, String usings) =>
        sourceTree.Write(
            ScratchPaths.GetNoteEndpoint,
            $$"""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;
            {{usings}}
            namespace ModularMonolith.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{noteId:guid}", GetAsync);

                private static async Task<IResult> GetAsync(
                    Guid noteId,
                    {{parameters}},
                    CancellationToken cancellationToken)
                {
            {{body}}
                }
            }

            """);
}
