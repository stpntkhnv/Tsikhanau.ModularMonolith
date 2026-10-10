using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

internal sealed class GetPongSourceEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet("/{pongId:guid}/source", GetAsync)
            .WithName("Pongs.GetPongSource");

    private static Task<Results<Ok<PongSourceDto>, ValidationProblem, ProblemHttpResult>> GetAsync(
        Guid pongId,
        IPongsModule pongsModule,
        CancellationToken cancellationToken) =>
        pongsModule
            .ExecuteQueryAsync(new GetPongSourceQuery(pongId), cancellationToken)
            .ToOkAsync();
}
