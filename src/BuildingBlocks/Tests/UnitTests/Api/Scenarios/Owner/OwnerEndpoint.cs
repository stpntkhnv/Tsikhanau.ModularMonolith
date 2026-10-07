using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api.Scenarios.Owner;

internal sealed class OwnerEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/owner", () => "owner");
}
