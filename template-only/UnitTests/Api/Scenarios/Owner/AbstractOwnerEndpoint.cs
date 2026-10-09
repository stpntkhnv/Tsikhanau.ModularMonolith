using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.UnitTests.Api.Scenarios.Owner;

internal abstract class AbstractOwnerEndpoint : IEndpoint
{
    public abstract void Map(IEndpointRouteBuilder endpoints);
}
