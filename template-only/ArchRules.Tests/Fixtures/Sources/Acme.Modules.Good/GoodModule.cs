using Acme.Modules.Good.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;

namespace Acme.Modules.Good;

public sealed class GoodModule : Module
{
    public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup("/widgets")
            .MapEndpointsOf<GoodModule>();

    protected override void AddServices(IServiceCollection services) => AddSettings<GoodSettings>(services);
}
