using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

internal sealed class GetPongSourceEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{pongId:guid}/source", GetAsync);

    private static Task<IResult> GetAsync(Guid pongId, IPongsModule pongsModule, CancellationToken cancellationToken) =>
        pongsModule
            .ExecuteQueryAsync(new GetPongSourceQuery(pongId), cancellationToken)
            .ToHttpResultAsync();
}
