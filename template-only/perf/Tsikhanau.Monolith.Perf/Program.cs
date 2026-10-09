using System.Globalization;
using Tsikhanau.Monolith.Perf.Running;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return await ScenarioRunner.RunAsync(args);
