using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal readonly record struct PingSchedule : IValueObject
{
    public const Int32 MaxRepeats = 100;

    public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(1);

    public static readonly PingSchedule Default = new(TimeSpan.FromMinutes(1), 3);

    private PingSchedule(TimeSpan interval, Int32 repeats)
    {
        Interval = interval;
        Repeats = repeats;
    }

    public TimeSpan Interval { get; }

    public Int32 Repeats { get; }

    public static Result<PingSchedule> Create(TimeSpan interval, Int32 repeats, String fieldName) =>
        Result
            .Collect(ValidateInterval(interval, $"{fieldName}Interval"))
            .And(ValidateRepeats(repeats, $"{fieldName}Repeats"))
            .Map((validInterval, validRepeats) => new PingSchedule(validInterval, validRepeats));

    private static Result<TimeSpan> ValidateInterval(TimeSpan interval, String fieldName) =>
        interval > TimeSpan.Zero && interval <= MaxInterval
            ? interval
            : Error.Validation(fieldName, $"Interval must be above zero and at most {MaxInterval}");

    private static Result<Int32> ValidateRepeats(Int32 repeats, String fieldName) =>
        repeats is > 0 and <= MaxRepeats
            ? repeats
            : Error.Validation(fieldName, $"Repeats must be 1 to {MaxRepeats}");
}
