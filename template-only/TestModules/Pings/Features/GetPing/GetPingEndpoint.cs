using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.GetPing;

internal sealed class GetPingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet("/{pingId:guid}", GetAsync)
            .WithName("Pings.GetPing");

    private static Task<Results<Ok<PingDto>, ValidationProblem, ProblemHttpResult>> GetAsync(
        Guid pingId,
        IPingsModule pingsModule,
        CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteQueryAsync(new GetPingQuery(pingId), cancellationToken)
            .ToOkAsync();
}
