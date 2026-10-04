using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class ModuleDbContextOptions
{
    public const String MigrationsHistoryTable = "__ef_migrations_history";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder dbContextOptionsBuilder,
        NpgsqlDataSource npgsqlDataSource,
        String schema) =>
        dbContextOptionsBuilder
            .UseNpgsql(
                npgsqlDataSource,
                npgsqlOptionsBuilder => npgsqlOptionsBuilder.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention();

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder dbContextOptionsBuilder,
        String connectionString,
        String schema) =>
        dbContextOptionsBuilder
            .UseNpgsql(
                connectionString,
                npgsqlOptionsBuilder => npgsqlOptionsBuilder.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention();
}
