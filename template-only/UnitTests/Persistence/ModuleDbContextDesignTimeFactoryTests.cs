using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.UnitTests.Persistence;

public sealed class ModuleDbContextDesignTimeFactoryTests
{
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

    private static IModel CreateDesignTimeModel<TDbContext>(IDesignTimeDbContextFactory<TDbContext> factory)
        where TDbContext : DbContext
    {
        using var dbContext = factory.CreateDbContext([]);

        return dbContext.GetService<IDesignTimeModel>().Model;
    }
}
