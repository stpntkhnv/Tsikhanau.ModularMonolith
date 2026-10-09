using System.ComponentModel.DataAnnotations;
using Tsikhanau.Monolith.Application.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Database;

public sealed record DatabaseSettings
{
    public const String Section = $"{SettingsRoot.Section}:Database";

    internal const Int32 MinConnectionsPerModule = 2;

    [Range(1, 10_000)]
    public required Int32 MaxConnections { get; init; }

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public required TimeSpan PrepareTimeout { get; init; }

    public Int32 CalculatePoolSizePerModule(Int32 moduleCount) =>
        Math.Max(MinConnectionsPerModule, MaxConnections / moduleCount);
}
