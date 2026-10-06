using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pings.Domain.Pings;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Persistence;

public sealed class DbContextPoolTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Module_context_comes_back_from_the_pool_with_its_defaults_and_nothing_tracked()
    {
        using var host = TestModulesHost.BuildUnstarted(postgresFixture.ConnectionString);
        ModuleDbContext usedDbContext;
        await using (var usedScope = host.Services.CreateAsyncScope())
        {
            usedDbContext = ResolveDbContext(usedScope);
            ChangeWhatQueuesAndTheUnitOfWorkChange(usedDbContext);
        }

        await using var scope = host.Services.CreateAsyncScope();

        var dbContext = ResolveDbContext(scope);

        dbContext.ShouldBeSameAs(usedDbContext);
        dbContext.Database.AutoSavepointsEnabled.ShouldBeTrue();
        dbContext.ChangeTracker.AutoDetectChangesEnabled.ShouldBeTrue();
        dbContext.Database
            .GetCommandTimeout()
            .ShouldBeNull();

        dbContext.ChangeTracker
            .Entries()
            .ShouldBeEmpty();
    }

    private static ModuleDbContext ResolveDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider
            .GetRequiredService<ModuleContext<IPingsModule>>()
            .DbContext;

    private static void ChangeWhatQueuesAndTheUnitOfWorkChange(ModuleDbContext dbContext)
    {
        dbContext.Database.AutoSavepointsEnabled = false;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        dbContext.Add(CreatePing());
    }

    private static Ping CreatePing()
    {
        var createdOn = TestModulesHost.TestStartedOn;
        return Ping.Create(PingId.New(createdOn), PingLabel.Create("pooled", "label").Value, createdOn);
    }
}
