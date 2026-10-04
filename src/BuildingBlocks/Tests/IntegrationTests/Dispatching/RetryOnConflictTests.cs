using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.TouchPing;
using ModularMonolith.TestModules.Pings.Features.TouchPingWithRetry;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Dispatching;

public sealed class RetryOnConflictTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Marked_command_that_loses_a_race_runs_again_and_succeeds()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("retried");
        TouchFromOutsideAfterFirstLoad(testModulesHost, pingId);

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new TouchPingWithRetryCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        result.IsSuccess.ShouldBeTrue();
        ping.Touches.ShouldBe(2);
        ping.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Command_without_the_marker_still_answers_the_conflict()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("not retried");
        TouchFromOutsideAfterFirstLoad(testModulesHost, pingId);

        var result = await testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        result.Error.ShouldBe(UnitOfWorkErrors.ConcurrencyConflict);
        ping.Touches.ShouldBe(1);
    }

    [Fact]
    public async Task Marked_command_gives_up_after_three_attempts_and_answers_the_conflict()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("always raced");
        TouchFromOutsideAfterEveryLoad(testModulesHost, pingId);

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new TouchPingWithRetryCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        result.Error.ShouldBe(UnitOfWorkErrors.ConcurrencyConflict);
        ping.Touches.ShouldBe(3);
    }

    private static void TouchFromOutsideAfterFirstLoad(TestModulesHost testModulesHost, Guid pingId)
    {
        var hasRaced = 0;
        testModulesHost.PingHooks.AfterLoad = async () =>
        {
            if (Interlocked.Exchange(ref hasRaced, 1) == 0)
            {
                await TouchFromOutsideAsync(testModulesHost, pingId);
            }
        };
    }

    private static void TouchFromOutsideAfterEveryLoad(TestModulesHost testModulesHost, Guid pingId)
    {
        var isRacing = 0;
        testModulesHost.PingHooks.AfterLoad = async () =>
        {
            if (Interlocked.Exchange(ref isRacing, 1) == 0)
            {
                await TouchFromOutsideAsync(testModulesHost, pingId);
                Volatile.Write(ref isRacing, 0);
            }
        };
    }

    private static async Task TouchFromOutsideAsync(TestModulesHost testModulesHost, Guid pingId)
    {
        var touchResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new TouchPingCommand(pingId),
            CancellationToken);

        touchResult.IsSuccess.ShouldBeTrue();
    }
}
