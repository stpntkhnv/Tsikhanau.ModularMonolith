using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.UnitTests.Persistence;

public sealed class ModuleDbContextDesignTimeFactoryTests
{
    private const String SchemaProblem = "must name its schema in a const String SchemaName";

    private const String ConstructorProblem = "must have a constructor taking only DbContextOptions";

    [Fact]
    public void Pings_model_is_built_without_settings_environment_variables_or_a_database()
    {
        var model = CreateDesignTimeModel(new PingsDbContextDesignTimeFactory());
        var defaultSchema = model.GetDefaultSchema();
        var pingEntityType = model.FindEntityType(typeof(Ping));

        defaultSchema.ShouldBe(PingsDbContext.SchemaName);
        pingEntityType.ShouldNotBeNull();
    }

    [Fact]
    public void Pongs_model_is_built_without_settings_environment_variables_or_a_database()
    {
        var model = CreateDesignTimeModel(new PongsDbContextDesignTimeFactory());
        var defaultSchema = model.GetDefaultSchema();
        var pongEntityType = model.FindEntityType(typeof(Pong));

        defaultSchema.ShouldBe(PongsDbContext.SchemaName);
        pongEntityType.ShouldNotBeNull();
    }

    [Fact]
    public void A_one_line_factory_builds_the_model_in_the_schema_its_db_context_names()
    {
        var model = CreateDesignTimeModel(new BasketsDbContextDesignTimeFactory());
        var defaultSchema = model.GetDefaultSchema();

        defaultSchema.ShouldBe(BasketsDbContext.SchemaName);
    }

    [Fact]
    public void A_one_line_factory_keeps_the_migration_history_in_the_schema_its_db_context_names()
    {
        var relationalOptions = GetRelationalOptions(new BasketsDbContextDesignTimeFactory());

        relationalOptions.MigrationsHistoryTableName.ShouldBe(ModuleDbContextOptions.MigrationsHistoryTable);
        relationalOptions.MigrationsHistoryTableSchema.ShouldBe(BasketsDbContext.SchemaName);
    }

    [Fact]
    public void A_factory_of_a_db_context_without_a_schema_name_fails_with_a_message_that_names_the_db_context()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new SchemalessDbContextDesignTimeFactory().CreateDbContext([]));

        exception.Message.ShouldBe($"Module db context {typeof(SchemalessDbContext).FullName} {SchemaProblem}");
    }

    [Fact]
    public void A_factory_of_a_db_context_with_another_constructor_fails_with_a_message_that_names_the_one_it_needs()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ClockedDbContextDesignTimeFactory().CreateDbContext([]));

        exception.InnerException.ShouldBeOfType<MissingMethodException>();
        exception.Message.ShouldBe(
            $"Module db context {typeof(ClockedDbContext).FullName} {ConstructorProblem}<ClockedDbContext>");
    }

    private static IModel CreateDesignTimeModel<TDbContext>(IDesignTimeDbContextFactory<TDbContext> factory)
        where TDbContext : DbContext
    {
        using var dbContext = factory.CreateDbContext([]);

        return dbContext.GetService<IDesignTimeModel>().Model;
    }

    private static RelationalOptionsExtension GetRelationalOptions<TDbContext>(
        IDesignTimeDbContextFactory<TDbContext> factory)
        where TDbContext : DbContext
    {
        using var dbContext = factory.CreateDbContext([]);

        return RelationalOptionsExtension.Extract(dbContext.GetService<IDbContextOptions>());
    }

    private sealed class BasketsDbContext(DbContextOptions<BasketsDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "baskets";
    }

    private sealed class BasketsDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<BasketsDbContext>;

    private sealed class SchemalessDbContext(DbContextOptions<SchemalessDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions);

    private sealed class SchemalessDbContextDesignTimeFactory
        : ModuleDbContextDesignTimeFactory<SchemalessDbContext>;

    private sealed class ClockedDbContext(
        DbContextOptions<ClockedDbContext> dbContextOptions,
        TimeProvider timeProvider)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "clocked";

        public TimeProvider TimeProvider { get; } = timeProvider;
    }

    private sealed class ClockedDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<ClockedDbContext>;
}
