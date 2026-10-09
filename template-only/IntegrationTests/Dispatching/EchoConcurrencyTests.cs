using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Features.AddEcho;
using Tsikhanau.Monolith.TestModules.Pings.Features.RemoveEcho;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Dispatching;

public sealed class EchoConcurrencyTests(PostgresFixture postgresFixture)
{
    private const String EchoCountSql = "SELECT count(*) FROM pings.echoes WHERE ping_id = @PingId";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_echo_additions_end_in_one_conflict()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("echoed");
        testModulesHost.SetLoadBarrier(2);

        var results = await Task.WhenAll(
            testModulesHost.Pings.ExecuteCommandAsync(
                new AddEchoCommand(pingId, Guid.CreateVersion7(), "a"),
                CancellationToken),
            testModulesHost.Pings.ExecuteCommandAsync(
                new AddEchoCommand(pingId, Guid.CreateVersion7(), "b"),
                CancellationToken));

        var ping = await testModulesHost.GetPingAsync(pingId);
        var echoCount = await CountEchoesAsync(testModulesHost, pingId);

        ShouldEndInOneConflict(results);
        echoCount.ShouldBe(1);
        ping.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_removals_of_different_echoes_end_in_one_conflict()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("pruned");
        var firstEchoId = Guid.CreateVersion7();
        var secondEchoId = Guid.CreateVersion7();
        var firstAddResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new AddEchoCommand(pingId, firstEchoId, "a"),
            CancellationToken);

        var secondAddResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new AddEchoCommand(pingId, secondEchoId, "b"),
            CancellationToken);

        testModulesHost.SetLoadBarrier(2);

        var results = await Task.WhenAll(
            testModulesHost.Pings.ExecuteCommandAsync(new RemoveEchoCommand(pingId, firstEchoId), CancellationToken),
            testModulesHost.Pings.ExecuteCommandAsync(new RemoveEchoCommand(pingId, secondEchoId), CancellationToken));

        var ping = await testModulesHost.GetPingAsync(pingId);
        var echoCount = await CountEchoesAsync(testModulesHost, pingId);

        firstAddResult.IsSuccess.ShouldBeTrue();
        secondAddResult.IsSuccess.ShouldBeTrue();
        ShouldEndInOneConflict(results);
        echoCount.ShouldBe(1);
        ping.Version.ShouldBe(4);
    }

    private static void ShouldEndInOneConflict(Result<Unit>[] results)
    {
        results
            .Count(result => result.IsSuccess)
            .ShouldBe(1);

        results
            .Single(result => result.IsFailure).Error
            .ShouldBe(UnitOfWorkErrors.ConcurrencyConflict);
    }

    private static Task<Int64> CountEchoesAsync(TestModulesHost testModulesHost, Guid pingId) =>
        testModulesHost.Database.ExecuteScalarAsync<Int64>(EchoCountSql, new { PingId = pingId });
}
