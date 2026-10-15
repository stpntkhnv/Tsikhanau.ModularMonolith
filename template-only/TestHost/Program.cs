using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.TestHost.Probes;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;

return await MonolithHost.RunAsync(args, AddServices);

static void AddServices(WebApplicationBuilder builder)
{
    builder.AddServiceDefaults(AppDiagnostics.SourceName);
    builder.EnsureConnectionString();
    builder.Services.AddPlatformServices(builder.Configuration);
    builder.AddModule<PingsModule>();
    builder.AddModule<PongsModule>();
    builder.AddProcessProbes();
}

public partial class Program;
