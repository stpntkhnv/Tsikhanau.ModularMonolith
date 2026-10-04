using System.Diagnostics;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;

public static class AppDiagnostics
{
    public const String SourceName = "ModularMonolith";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
