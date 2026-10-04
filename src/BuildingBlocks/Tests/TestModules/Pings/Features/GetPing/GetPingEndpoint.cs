using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.GetPing;

internal sealed class GetPingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/pings/{pingId:guid}", GetAsync);

    private static Task<IResult> GetAsync(Guid pingId, IPingsModule pingsModule, CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteQueryAsync(new GetPingQuery(pingId), cancellationToken)
            .ToHttpResultAsync();
}
