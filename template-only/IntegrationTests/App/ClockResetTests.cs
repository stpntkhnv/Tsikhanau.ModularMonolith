using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class ClockResetTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private static readonly TimeZoneInfo MovedTimeZone =
        TimeZoneInfo.CreateCustomTimeZone("moved", TimeSpan.FromHours(3), "moved", "moved");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Clock_and_facade_resolved_before_a_reset_see_the_test_start_after_it()
    {
        var timeProvider = App.Services.GetRequiredService<TimeProvider>();
        var pingsModule = App.Services.GetRequiredService<IPingsModule>();
        var pingId = Guid.CreateVersion7();
        MoveClock(TimeSpan.FromDays(3));
        var movedOn = timeProvider.GetUtcNow();

        await App.ResetAsync();
        var resetOn = timeProvider.GetUtcNow();
        var localTimeZone = timeProvider.LocalTimeZone;
        var created = await pingsModule.ExecuteCommandAsync(
            new CreatePingCommand(pingId, "clock", Optional<String>.None()),
            CancellationToken);

        var outboxRows = await App.Database.ReadQueueRowsAsync(App.GetModule("Pings").Schema, OutboxMessage.Table);

        movedOn.ShouldBeGreaterThanOrEqualTo(TestAppFixture.TestStartedOn.AddDays(3));
        resetOn.ShouldBe(TestAppFixture.TestStartedOn);
        localTimeZone.ShouldBe(TimeZoneInfo.Utc);
        created.ShouldBe(Result<Guid>.Success(pingId));
        outboxRows
            .ShouldHaveSingleItem().NextAttemptOn
            .ShouldBe(TestAppFixture.TestStartedOn.UtcDateTime);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Clock_is_back_at_the_test_start_whatever_the_previous_test_did(Int32 movedDays)
    {
        var clockState = new ClockState(App.Time.GetUtcNow(), App.Time.AutoAdvanceAmount, App.Time.LocalTimeZone);
        MoveClock(TimeSpan.FromDays(movedDays));

        clockState.ShouldBe(new ClockState(TestAppFixture.TestStartedOn, TimeSpan.Zero, TimeZoneInfo.Utc));
    }

    private void MoveClock(TimeSpan movedBy)
    {
        App.Time.Advance(movedBy);
        App.Time.AutoAdvanceAmount = TimeSpan.FromSeconds(1);
        App.Time.SetLocalTimeZone(MovedTimeZone);
    }

    private sealed record ClockState(DateTimeOffset ReadOn, TimeSpan AutoAdvanceAmount, TimeZoneInfo LocalTimeZone);
}
