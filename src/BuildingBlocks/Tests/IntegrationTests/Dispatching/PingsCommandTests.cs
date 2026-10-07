using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using ModularMonolith.TestModules.Pings.Features.CreatePing;
using ModularMonolith.TestModules.Pings.Features.LoopPing;
using ModularMonolith.TestModules.Pings.Features.RejectAfterTouch;
using ModularMonolith.TestModules.Pings.Features.RelayEcho;
using ModularMonolith.TestModules.Pings.Features.ReschedulePing;
using ModularMonolith.TestModules.Pings.Features.ThrowAfterTouch;
using ModularMonolith.TestModules.Pings.Features.TouchPing;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Dispatching;

public sealed class PingsCommandTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Created_ping_is_readable_with_version_one()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = Guid.CreateVersion7();

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new CreatePingCommand(pingId, "hello"),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);

        result.Value.ShouldBe(pingId);
        ping.ShouldBe(new PingDto(pingId, "hello", 0, 1));
    }

    [Fact]
    public async Task Invalid_input_returns_every_field_error_at_once()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new CreatePingCommand(Guid.Empty, " "),
            CancellationToken);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.FieldErrors
            .Select(fieldError => fieldError.Field)
            .ShouldBe(["pingId", "label"], ignoreOrder: true);
    }

    [Fact]
    public async Task Invalid_composite_value_returns_an_error_for_each_of_its_fields()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("unscheduled");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new ReschedulePingCommand(pingId, TimeSpan.Zero, PingSchedule.MaxRepeats + 1),
            CancellationToken);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.FieldErrors
            .Select(fieldError => fieldError.Field)
            .ShouldBe(["scheduleInterval", "scheduleRepeats"], ignoreOrder: true);
    }

    [Fact]
    public async Task Domain_rule_violation_returns_the_named_error()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("tired");
        for (var touchNumber = 0; touchNumber < Ping.MaxTouches; touchNumber++)
        {
            var touchResult = await testModulesHost.Pings.ExecuteCommandAsync(
                new TouchPingCommand(pingId),
                CancellationToken);

            touchResult.IsSuccess.ShouldBeTrue();
        }

        var result = await testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken);

        result.Error.ShouldBe(PingErrors.TooManyTouches);
    }

    [Fact]
    public async Task Failed_command_saves_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("rejected");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new RejectAfterTouchCommand(pingId),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);

        result.Error.ShouldBe(PingErrors.Rejected);
        ping.Touches.ShouldBe(0);
    }

    [Fact]
    public async Task Throwing_command_surfaces_the_exception_and_saves_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("thrown");

        await Should.ThrowAsync<InvalidOperationException>(
            () => testModulesHost.Pings.ExecuteCommandAsync(new ThrowAfterTouchCommand(pingId), CancellationToken));

        var ping = await testModulesHost.GetPingAsync(pingId);

        ping.Touches.ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_changes_to_one_aggregate_end_in_one_conflict()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("contested");
        testModulesHost.SetLoadBarrier(2);

        var results = await Task.WhenAll(
            testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken),
            testModulesHost.Pings.ExecuteCommandAsync(new TouchPingCommand(pingId), CancellationToken));

        var ping = await testModulesHost.GetPingAsync(pingId);

        results
            .Count(result => result.IsSuccess)
            .ShouldBe(1);

        results
            .Single(result => result.IsFailure).Error
            .ShouldBe(UnitOfWorkErrors.ConcurrencyConflict);

        ping.Touches.ShouldBe(1);
    }

    [Fact]
    public async Task Domain_events_settling_in_ten_rounds_are_saved()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("settles");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new LoopPingCommand(pingId, 10),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);

        result.IsSuccess.ShouldBeTrue();
        ping.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Domain_events_beyond_ten_rounds_stop_and_save_nothing()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("loop");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new LoopPingCommand(pingId, 11),
            CancellationToken);

        var ping = await testModulesHost.GetPingAsync(pingId);

        result.Error.ShouldBe(UnitOfWorkErrors.EventLoop(roundCount: 10));
        ping.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Change_a_domain_event_handler_makes_to_another_ping_is_saved_and_moves_its_version()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("relaying");
        var targetPingId = await testModulesHost.CreatePingAsync("target");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new RelayEchoCommand(pingId, targetPingId, Guid.CreateVersion7(), "relayed"),
            CancellationToken);

        var targetPing = await testModulesHost.GetPingAsync(targetPingId);
        var targetEchoTexts = await testModulesHost.Database.QueryAsync<String>(
            "SELECT text FROM pings.echoes WHERE ping_id = @PingId",
            new { PingId = targetPingId });

        result.IsSuccess.ShouldBeTrue();
        targetEchoTexts.ShouldBe(["relayed"]);
        targetPing.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Known_unique_constraint_maps_to_its_error()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        await testModulesHost.CreatePingAsync("twin");

        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new CreatePingCommand(Guid.CreateVersion7(), "twin"),
            CancellationToken);

        result.Error.ShouldBe(PingErrors.LabelTaken);
    }

    [Fact]
    public async Task Missing_ping_query_returns_not_found()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var result = await testModulesHost.Pings.ExecuteQueryAsync(
            new GetPingQuery(Guid.CreateVersion7()),
            CancellationToken);

        result.Error.ShouldBe(PingErrors.NotFound);
    }
}
