using ModularMonolith.Testing.Database;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;
using Tsikhanau.Monolith.TestModules.Pings.Features.TouchPing;

namespace Tsikhanau.Monolith.IntegrationTests.InternalCommands;

public sealed class InternalCommandTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scheduled_command_runs_from_the_queue()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("later");
        await testModulesHost.Pings.ExecuteCommandAsync(new ScheduleTouchCommand(pingId), CancellationToken);

        var pingBefore = await testModulesHost.GetPingAsync(pingId);
        var processedCount = await testModulesHost.RunInternalCommandsAsync();
        var pingAfter = await testModulesHost.GetPingAsync(pingId);
        var rows = await testModulesHost.ReadInternalCommandRowsAsync();

        pingBefore.Touches.ShouldBe(0);
        processedCount.ShouldBe(1);
        pingAfter.Touches.ShouldBe(1);
        var row = rows.ShouldHaveSingleItem();
        row.IsProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task Scheduled_command_is_stored_under_its_stable_name()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("named");

        await testModulesHost.Pings.ExecuteCommandAsync(new ScheduleTouchCommand(pingId), CancellationToken);

        var rows = await testModulesHost.ReadInternalCommandRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.Type.ShouldBe("touch-ping");
    }

    [Fact]
    public async Task Failing_scheduled_command_is_marked_failed_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("worn");
        for (var touchNumber = 0; touchNumber < Ping.MaxTouches; touchNumber++)
        {
            await testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken);
        }

        await testModulesHost.Pings.ExecuteCommandAsync(new ScheduleTouchCommand(pingId), CancellationToken);

        await testModulesHost.RunInternalCommandsAsync();

        var rows = await testModulesHost.ReadInternalCommandRowsAsync();
        var ping = await testModulesHost.GetPingAsync(pingId);

        var row = rows.ShouldHaveSingleItem();
        row.HasFailed.ShouldBeTrue();
        row.Attempts.ShouldBe(1);
        ping.Touches.ShouldBe(Ping.MaxTouches);
    }

    [Fact]
    public async Task Concurrency_conflict_in_a_scheduled_command_is_retried()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("raced");
        await testModulesHost.Pings.ExecuteCommandAsync(new ScheduleTouchCommand(pingId), CancellationToken);
        var hasRaced = 0;
        testModulesHost.PingHooks.AfterLoad = async () =>
        {
            if (Interlocked.Exchange(ref hasRaced, 1) == 0)
            {
                var touchResult = await testModulesHost.Pings.ExecuteCommandAsync(
                    new TouchPingCommand(pingId),
                    CancellationToken);

                touchResult.IsSuccess.ShouldBeTrue();
            }
        };

        var ranOn = testModulesHost.Time.GetUtcNow();

        await testModulesHost.RunInternalCommandsAsync();

        var rows = await testModulesHost.ReadInternalCommandRowsAsync();

        var row = rows.ShouldHaveSingleItem();
        row.Attempts.ShouldBe(1);
        row.HasFailed.ShouldBeFalse();
        row.NextAttemptOn.ShouldBe(ranOn.UtcDateTime.AddSeconds(1));

        testModulesHost.PingHooks.AfterLoad = static () => Task.CompletedTask;
        testModulesHost.Time.Advance(TimeSpan.FromSeconds(1));

        await testModulesHost.RunInternalCommandsAsync();

        var retriedRows = await testModulesHost.ReadInternalCommandRowsAsync();
        var ping = await testModulesHost.GetPingAsync(pingId);

        var retriedRow = retriedRows.ShouldHaveSingleItem();
        retriedRow.IsProcessed.ShouldBeTrue();
        ping.Touches.ShouldBe(2);
    }

    [Fact]
    public async Task Unknown_command_type_is_retried_then_fails()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.Database.ExecuteAsync(
            """
            INSERT INTO pings.internal_commands (id, type, payload, attempts, next_attempt_on, enqueued_on)
            VALUES (@id, 'command-from-a-newer-release', '{}'::jsonb, 0, @enqueuedOn, @enqueuedOn)
            """,
            new { id = Guid.CreateVersion7(), enqueuedOn = testModulesHost.Time.GetUtcNow() });

        await testModulesHost.RunInternalCommandsAsync();

        var rowsAfterFirstAttempt = await testModulesHost.ReadInternalCommandRowsAsync();

        foreach (var seconds in new[] { 1, 10, 60, 300, 900, 1800 })
        {
            testModulesHost.Time.Advance(TimeSpan.FromSeconds(seconds));
            await testModulesHost.RunInternalCommandsAsync();
        }

        var rowsAfterLastAttempt = await testModulesHost.ReadInternalCommandRowsAsync();

        var firstAttemptRow = rowsAfterFirstAttempt.ShouldHaveSingleItem();
        firstAttemptRow.HasFailed.ShouldBeFalse();
        firstAttemptRow.Attempts.ShouldBe(1);
        var lastAttemptRow = rowsAfterLastAttempt.ShouldHaveSingleItem();
        lastAttemptRow.HasFailed.ShouldBeTrue();
        lastAttemptRow.Attempts.ShouldBe(7);
    }

    [Fact]
    public async Task Module_queues_take_turns()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("first");
        await testModulesHost.CreatePingAsync("second");
        await testModulesHost.CreatePingAsync("third");
        await testModulesHost.Pings.ExecuteCommandAsync(new ScheduleTouchCommand(pingId), CancellationToken);
        var deliveredCountBeforeTouch = -1;
        testModulesHost.PingHooks.AfterLoad = async () =>
            deliveredCountBeforeTouch = await testModulesHost.Database.ExecuteScalarAsync<Int32>(
                "SELECT count(*)::int FROM pings.outbox_messages WHERE processed_on IS NOT NULL");

        var processedCount = await testModulesHost.RunPingsQueuesAsync();

        processedCount.ShouldBe(5);
        deliveredCountBeforeTouch.ShouldBe(1);
    }
}
