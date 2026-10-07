using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Api.Modules;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api.Scenarios.Owner;

internal sealed class OwnerModule : Module
{
    public override String Name => "Owner";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapEndpointsOf<OwnerModule>();

    protected override void AddServices(IServiceCollection services) { }
}
