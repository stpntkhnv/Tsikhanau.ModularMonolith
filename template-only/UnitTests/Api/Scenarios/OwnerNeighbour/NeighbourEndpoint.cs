using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.UnitTests.Api.Scenarios.OwnerNeighbour;

internal sealed class NeighbourEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/neighbour", () => "neighbour");
}
