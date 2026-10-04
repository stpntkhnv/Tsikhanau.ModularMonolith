using Microsoft.AspNetCore.Routing;

namespace ModularMonolith.BuildingBlocks.Api.Endpoints;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder endpoints);
}
