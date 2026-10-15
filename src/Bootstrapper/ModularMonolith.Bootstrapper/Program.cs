using ModularMonolith.Bootstrapper.Platform;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

return await MonolithHost.RunAsync(args, AddServices);

static void AddServices(WebApplicationBuilder builder)
{
    builder.AddServiceDefaults(AppDiagnostics.SourceName);
    builder.AddPlatform();
}

public partial class Program;
