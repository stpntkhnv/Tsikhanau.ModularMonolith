using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;

namespace Acme.Modules.Bad;

public sealed class BadModule : Module
{
    public override String Name => "Bad";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup("/crates")
            .MapEndpointsOf<BadModule>();

    protected override void AddServices(IServiceCollection services) { }
}
