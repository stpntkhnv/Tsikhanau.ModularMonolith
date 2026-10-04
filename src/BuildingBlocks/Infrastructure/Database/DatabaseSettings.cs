using System.ComponentModel.DataAnnotations;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

public sealed record DatabaseSettings
{
    public const String Section = "ModularMonolith:Database";

    [Range(1, 10_000)]
    public required Int32 MaxConnections { get; init; }

    public Int32 CalculatePoolSizePerModule(Int32 moduleCount) => Math.Max(1, MaxConnections / moduleCount);
}
