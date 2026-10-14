using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class ModuleBoundaryMutations(SourceTree sourceTree)
{
    private const String TagsProject = $"{ScratchPaths.Tags}/ModularMonolith.Modules.Tags.csproj";

    private const String TagsModule = $"{ScratchPaths.Tags}/TagsModule.cs";

    private const String TagsModuleOpening = "public sealed class TagsModule : Module\n{\n";

    private const String PongsHandler =
        "template-only/TestModules/Pongs/Features/PingCreated/PingCreatedIntegrationEventHandler.cs";

    private const String BootstrapperProgram = "src/Bootstrapper/ModularMonolith.Bootstrapper/Program.cs";

    private const String NeighbourMember =
        "    public String Neighbour => new ModularMonolith.Modules.Notes.NotesModule().Name;\n\n";

    private const String NotesContractsReference = """
        <ProjectReference Include="..\..\Notes\ModularMonolith.Modules.Notes.Contracts\ModularMonolith.Modules.Notes.Contracts.csproj" />
        """;

    private const String NotesModuleReference = """
        <ProjectReference Include="..\..\Notes\ModularMonolith.Modules.Notes\ModularMonolith.Modules.Notes.csproj" />
        """;

    public Result<Unit> ModuleReferencesOtherModuleProject() =>
        sourceTree
            .Replace(TagsProject, NotesContractsReference, $"{NotesContractsReference}\n    {NotesModuleReference}")
            .Bind(_ => sourceTree.Replace(TagsModule, TagsModuleOpening, TagsModuleOpening + NeighbourMember));

    public Result<Unit> TestModuleUsesOtherModuleDomain() =>
        sourceTree.Replace(
            PongsHandler,
            "        var pongId = PongId.From(pingId);",
            """
                    var otherModulePingId = Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.PingId.From(pingId);
                    var pongId = PongId.From(otherModulePingId.Value);
            """);

    public Result<Unit> HandlerForForeignQuery() =>
        sourceTree.Write(
            $"{ScratchPaths.Tags}/Features/NoteCreated/GetNoteQueryHandler.cs",
            """
            using ModularMonolith.Modules.Notes.Contracts.Queries.GetNote;
            using Tsikhanau.Monolith.Application.Queries;
            using Tsikhanau.Railway;

            namespace ModularMonolith.Modules.Tags.Features.NoteCreated;

            internal sealed class GetNoteQueryHandler : IQueryHandler<GetNoteQuery, NoteDto>
            {
                public Task<Result<NoteDto>> Handle(GetNoteQuery query, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success(new NoteDto(query.NoteId, "hijacked")));
            }

            """);

    public Result<Unit> ModuleNotAddedToHost() =>
        sourceTree
            .Replace(BootstrapperProgram, "builder.AddModule<TagsModule>();\n", "")
            .Bind(_ => sourceTree.Replace(BootstrapperProgram, "using ModularMonolith.Modules.Tags;\n", ""));
}
