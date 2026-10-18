using System.Diagnostics;

namespace Acme.Modules.Bad.Domain.Stamps;

internal static class Stamp
{
    public static DateTimeOffset Now() => DateTimeOffset.UtcNow;

    public static DateTimeOffset Read(TimeProvider timeProvider) => timeProvider.GetUtcNow();

    public static DateTime Today() => DateTime.Today;

    public static Int32 Uptime() => Environment.TickCount;

    public static Int64 LongUptime() => Environment.TickCount64;

    public static void StartLap() => Stopwatch.StartNew();

    public static Int64 TicksPerSecond() => Stopwatch.Frequency;
}
