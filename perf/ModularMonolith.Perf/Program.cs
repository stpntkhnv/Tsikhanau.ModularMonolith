using System.Globalization;
using ModularMonolith.Perf.Running;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return await ScenarioRunner.RunAsync(args);
