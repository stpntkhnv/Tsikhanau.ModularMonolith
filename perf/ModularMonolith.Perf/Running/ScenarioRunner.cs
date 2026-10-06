using System.Runtime;
using ModularMonolith.Perf.Measuring;
using ModularMonolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.Perf.Running;

internal static class ScenarioRunner
{
    private const Int32 UnknownScenarioExitCode = 1;
    private const Int32 CancelledExitCode = 130;
    private const String DynamicAdaptationModeVariable = "GCDynamicAdaptationMode";

    public static async Task<Int32> RunAsync(IReadOnlyList<String> arguments)
    {
        var scenarios = typeof(ScenarioRunner).Assembly
            .CreateInstancesOf<IScenario>()
            .ToList();

        var foundScenario = Optional<IScenario>.FromNullable(
            scenarios.Find(scenario => IsRequested(scenario, arguments)));

        if (foundScenario.IsNone)
        {
            Console.WriteLine($"scenarios: {String.Join(" | ", scenarios.Select(scenario => scenario.Name))}");
            return UnknownScenarioExitCode;
        }

        Console.WriteLine(BuildHeader(arguments));
        using var consoleCancellation = new ConsoleCancellation();
        try
        {
            await foundScenario.Value.RunAsync(PerfOptions.Parse(arguments.Skip(1)), consoleCancellation.Token);
            return 0;
        }
        catch (OperationCanceledException) when (consoleCancellation.Token.IsCancellationRequested)
        {
            Console.WriteLine("cancelled");
            return CancelledExitCode;
        }
    }

    private static Boolean IsRequested(IScenario scenario, IReadOnlyList<String> arguments) =>
        arguments.Count > 0 && scenario.Name == arguments[0];

    private static String BuildHeader(IReadOnlyList<String> arguments) =>
        $"# {String.Join(' ', arguments)} | cpus {Environment.ProcessorCount} | servergc {GCSettings.IsServerGC}"
            + $" | datas {ReadDynamicAdaptationMode()} | load {MachineLoad.Read()}";

    private static Object? ReadDynamicAdaptationMode() =>
        GC
            .GetConfigurationVariables()
            .GetValueOrDefault(DynamicAdaptationModeVariable);
}
