using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;

namespace Tsikhanau.Monolith.UnitTests.Api.Scenarios.Owner;

internal sealed class OwnerModule : Module
{
    public override String Name => "Owner";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapEndpointsOf<OwnerModule>();

    protected override void AddServices(IServiceCollection services) { }
}
