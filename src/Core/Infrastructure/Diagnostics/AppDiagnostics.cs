using System.Diagnostics;

namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

public static class AppDiagnostics
{
    public const String SourceName = "ModularMonolith";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
