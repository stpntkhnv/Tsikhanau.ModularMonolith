namespace ModularMonolith.Perf.Measuring;

internal static class MachineLoad
{
    private const String LoadAveragePath = "/proc/loadavg";

    public static String Read() =>
        File.Exists(LoadAveragePath)
            ? File
                .ReadAllText(LoadAveragePath)
                .Split(' ')[0]
            : "n/a";
}
