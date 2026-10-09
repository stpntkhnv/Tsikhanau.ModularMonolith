using Microsoft.AspNetCore.Routing;

namespace Tsikhanau.Monolith.Api.Endpoints;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder endpoints);
}
