using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

namespace Tsikhanau.Monolith.Perf.Pings;

internal static class SamplePing
{
    public static Ping Create(String label) =>
        Ping.Create(PingId.From(Guid.CreateVersion7()), CreateLabel(label), DateTimeOffset.UtcNow);

    private static PingLabel CreateLabel(String label) =>
        PingLabel
            .Create(label, "label")
            .GetValueOrThrow();
}
