using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;

namespace Acme.Modules.Peer;

public sealed class PeerModule : Module
{
    public override String Name => "Peer";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup("/peers")
            .MapEndpointsOf<PeerModule>();

    protected override void AddServices(IServiceCollection services) { }
}
