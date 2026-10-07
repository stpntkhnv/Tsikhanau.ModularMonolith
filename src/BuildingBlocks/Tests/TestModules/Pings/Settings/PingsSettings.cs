using System.ComponentModel.DataAnnotations;

namespace ModularMonolith.TestModules.Pings.Settings;

internal sealed record PingsSettings
{
    [Range(1, 100)]
    public required Int32 Limit { get; init; }
}
