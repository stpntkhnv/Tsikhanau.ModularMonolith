using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.TestHost.Probes;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;

return await MonolithHost.RunAsync(args, AddServices);

static void AddServices(WebApplicationBuilder builder)
{
    builder.AddModule<PingsModule>();
    builder.AddModule<PongsModule>();
    builder.AddProcessProbes();
}

public partial class Program;
