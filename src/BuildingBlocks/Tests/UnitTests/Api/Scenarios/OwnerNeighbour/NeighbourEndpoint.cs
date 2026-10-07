using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api.Scenarios.OwnerNeighbour;

internal sealed class NeighbourEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/neighbour", () => "neighbour");
}
