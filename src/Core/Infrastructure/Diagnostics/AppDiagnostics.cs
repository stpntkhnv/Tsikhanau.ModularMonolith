using System.Diagnostics;

namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

public static class AppDiagnostics
{
    public const String SourceName = "Tsikhanau.Monolith";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
