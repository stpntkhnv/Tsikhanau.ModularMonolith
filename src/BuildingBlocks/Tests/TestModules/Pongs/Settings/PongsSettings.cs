using System.ComponentModel.DataAnnotations;

namespace ModularMonolith.TestModules.Pongs.Settings;

internal sealed record PongsSettings
{
    [Range(1, 100)]
    public required Int32 Limit { get; init; }
}
