using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.RecordRejectedTouch;
using Tsikhanau.Monolith.TestModules.Pings.Features.RejectAfterTouch;
using Tsikhanau.Monolith.TestModules.Pings.Features.RejectUntouched;

namespace Tsikhanau.Monolith.IntegrationTests.Dispatching;

public sealed class CommitOnFailureTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Command_marked_to_commit_on_failure_keeps_its_changes_and_returns_its_error()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("kept");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new RecordRejectedTouchCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        var outboxRows = await testModulesHost.ReadOutboxRowsAsync();
        result.Error.ShouldBe(PingErrors.Rejected);
        ping.Touches.ShouldBe(1);
        ping.Version.ShouldBe(2);
        outboxRows.ShouldContain(row => row.Type == "pings.ping-touched");
    }

    [Fact]
    public async Task Marked_command_that_fails_without_changes_writes_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("untouched");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new RejectUntouchedCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        result.Error.ShouldBe(PingErrors.Rejected);
        ping.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Ordinary_command_that_fails_keeps_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("dropped");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new RejectAfterTouchCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);
        result.Error.ShouldBe(PingErrors.Rejected);
        ping.Touches.ShouldBe(0);
        ping.Version.ShouldBe(1);
    }
}
