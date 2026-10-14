using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class DomainDependencyMutations(SourceTree sourceTree)
{
    private const String NoteClass = "internal sealed class Note";

    private const String NoteFactory = "    public static Note Create(";

    private const String PingFactory = "    public static Ping Create(";

    private const String Ping = "template-only/TestModules/Pings/Domain/Pings/Ping.cs";

    private const String NoteUsings =
        "using ModularMonolith.Modules.Notes.Domain.Notes.Events;\nusing Tsikhanau.Monolith.Domain.Aggregates;";

    public Result<Unit> DomainUsesEfAttribute() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            NoteClass,
            $"[Microsoft.EntityFrameworkCore.Index(nameof(Title))]\n{NoteClass}");

    public Result<Unit> DomainMethodTakesDbContext() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            NoteFactory,
            """
                public static Int32 CountTracked(Microsoft.EntityFrameworkCore.DbContext dbContext) =>
                    dbContext.ChangeTracker
                        .Entries<Note>()
                        .Count();

                public static Note Create(
            """);

    public Result<Unit> TestModuleDomainMethodTakesDbContext() =>
        sourceTree.Replace(
            Ping,
            PingFactory,
            """
                public static Int32 CountTracked(Microsoft.EntityFrameworkCore.DbContext dbContext) =>
                    dbContext.ChangeTracker
                        .Entries<Ping>()
                        .Count();

                public static Ping Create(
            """);

    public Result<Unit> DomainUsesNpgsql() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            NoteFactory,
            """
                public static String Describe(Npgsql.NpgsqlConnection npgsqlConnection) => npgsqlConnection.Database;

                public static Note Create(
            """);

    public Result<Unit> DomainUsesApplicationLayer() =>
        sourceTree.Write(
            $"{ScratchPaths.Notes}/Domain/Notes/NoteConnections.cs",
            """
            using System.Data.Common;
            using Tsikhanau.Monolith.Application.Queries;

            namespace ModularMonolith.Modules.Notes.Domain.Notes;

            internal static class NoteConnections
            {
                public static Task<DbConnection> OpenAsync(
                    ISqlConnectionFactory<INotesModule> sqlConnectionFactory,
                    CancellationToken cancellationToken) =>
                    sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
            }

            """);

    public Result<Unit> DomainAttributePointsToEfAndInfrastructure() =>
        sourceTree
            .Replace(
                ScratchPaths.Note,
                NoteUsings,
                """
                using Microsoft.EntityFrameworkCore;
                using ModularMonolith.Modules.Notes.Domain.Notes.Events;
                using ModularMonolith.Modules.Notes.Infrastructure.Persistence;
                using Tsikhanau.Monolith.Domain.Aggregates;
                """)
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.Note,
                NoteClass,
                $"[EntityTypeConfiguration(typeof(NoteConfiguration))]\n{NoteClass}"));
}
