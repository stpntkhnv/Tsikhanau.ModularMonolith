using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api.Scenarios.Owner;

internal abstract class AbstractOwnerEndpoint : IEndpoint
{
    public abstract void Map(IEndpointRouteBuilder endpoints);
}
