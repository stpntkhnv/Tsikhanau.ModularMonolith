using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.TouchPing;

internal sealed class TouchPingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost("/{pingId:guid}/touch", TouchAsync)
            .WithName("Pings.TouchPing");

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> TouchAsync(
        Guid pingId,
        IPingsModule pingsModule,
        CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteCommandAsync(new TouchPingCommand(pingId), cancellationToken)
            .ToNoContentAsync();
}
