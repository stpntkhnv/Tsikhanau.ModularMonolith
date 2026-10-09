using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.UnitTests.Api.Scenarios.Owner;

internal sealed class OwnerEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/owner", () => "owner");
}
