using Microsoft.EntityFrameworkCore;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Persistence;

public sealed class MigrationTests
{
    [Fact]
    public void Pings_model_has_no_pending_changes()
    {
        using var dbContext = new PingsDbContextDesignTimeFactory().CreateDbContext([]);

        dbContext.Database
            .HasPendingModelChanges()
            .ShouldBeFalse();
    }

    [Fact]
    public void Pongs_model_has_no_pending_changes()
    {
        using var dbContext = new PongsDbContextDesignTimeFactory().CreateDbContext([]);

        dbContext.Database
            .HasPendingModelChanges()
            .ShouldBeFalse();
    }
}
