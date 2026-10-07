using ModularMonolith.Shared.Time;

namespace ModularMonolith.Shared.Tests.Time;

public sealed class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(90.5, 91)]
    [InlineData(90.4, 91)]
    [InlineData(90, 90)]
    [InlineData(0.001, 1)]
    public void Total_seconds_round_up_to_a_whole_second(Double seconds, Int32 wholeSeconds)
    {
        var roundedUp = TimeSpan.FromSeconds(seconds).TotalSecondsRoundedUp;

        roundedUp.ShouldBe(wholeSeconds);
    }
}
