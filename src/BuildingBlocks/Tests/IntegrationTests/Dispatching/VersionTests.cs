using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Features.AddEcho;
using ModularMonolith.TestModules.Pings.Features.InspectPing;
using ModularMonolith.TestModules.Pings.Features.RemoveEcho;
using ModularMonolith.TestModules.Pings.Features.TouchPing;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Dispatching;

public sealed class VersionTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_ping_is_saved_with_version_one_and_a_change_moves_it_to_two()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("counted");
        var createdPing = await testModulesHost.GetPingAsync(pingId);

        var touchResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new TouchPingCommand(pingId),
            CancellationToken);

        var touchedPing = await testModulesHost.GetPingAsync(pingId);

        createdPing.Version.ShouldBe(1);
        touchResult.IsSuccess.ShouldBeTrue();
        touchedPing.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Command_that_only_loads_the_ping_leaves_its_version_alone()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("inspected");

        var inspectResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new InspectPingCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);

        inspectResult.IsSuccess.ShouldBeTrue();
        ping.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Adding_and_removing_an_echo_moves_the_ping_version()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("echoed");
        var echoId = Guid.CreateVersion7();

        var addResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new AddEchoCommand(pingId, echoId, "hello"),
            CancellationToken);

        var pingAfterAdd = await testModulesHost.GetPingAsync(pingId);
        var removeResult = await testModulesHost.Pings.ExecuteCommandAsync(
            new RemoveEchoCommand(pingId, echoId),
            CancellationToken);

        var pingAfterRemove = await testModulesHost.GetPingAsync(pingId);

        addResult.IsSuccess.ShouldBeTrue();
        removeResult.IsSuccess.ShouldBeTrue();
        pingAfterAdd.Version.ShouldBe(2);
        pingAfterRemove.Version.ShouldBe(3);
    }
}
