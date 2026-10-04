using System.ComponentModel.DataAnnotations;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;

public sealed record CleanupSettings
{
    public const String Section = "ModularMonolith:Cleanup";

    public Boolean RunInBackground { get; init; } = true;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public required TimeSpan Interval { get; init; }

    [Range(typeof(TimeSpan), "01:00:00", "365.00:00:00")]
    public required TimeSpan KeepFor { get; init; }
}
