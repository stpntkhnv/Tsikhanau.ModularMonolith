using Acme.Modules.Bad.Domain.Crates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Acme.Modules.Bad.Features.ShowCrate;

internal sealed class ShowCrateEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{crateId:guid}", Show);

    private static String Show(Guid crateId, ICrateRepository crateRepository) => crateRepository.Describe(crateId);
}
