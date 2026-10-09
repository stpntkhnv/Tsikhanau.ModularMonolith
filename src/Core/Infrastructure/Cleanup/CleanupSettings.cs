using System.ComponentModel.DataAnnotations;
using Tsikhanau.Monolith.Application.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public sealed record CleanupSettings
{
    public const String Section = $"{SettingsRoot.Section}:Cleanup";

    public Boolean RunInBackground { get; init; } = true;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public required TimeSpan Interval { get; init; }

    [Range(typeof(TimeSpan), "01:00:00", "365.00:00:00")]
    public required TimeSpan KeepFor { get; init; }
}
