using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.GetPing;

internal sealed class GetPingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{pingId:guid}", GetAsync);

    private static Task<IResult> GetAsync(Guid pingId, IPingsModule pingsModule, CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteQueryAsync(new GetPingQuery(pingId), cancellationToken)
            .ToHttpResultAsync();
}
