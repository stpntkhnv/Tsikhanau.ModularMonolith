using Acme.Modules.Good;
using Acme.Modules.Peer;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Api.Modules;

namespace Acme.Bootstrapper;

public static class HostSetup
{
    public static IHostApplicationBuilder AddModules(IHostApplicationBuilder builder) =>
        builder
            .AddModule<GoodModule>()
            .AddModule<PeerModule>();
}
