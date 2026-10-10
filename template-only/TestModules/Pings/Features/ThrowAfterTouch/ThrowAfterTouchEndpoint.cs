using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ThrowAfterTouch;

internal sealed class ThrowAfterTouchEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost("/{pingId:guid}/throw", ThrowAsync)
            .WithName("Pings.ThrowAfterTouch");

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ThrowAsync(
        Guid pingId,
        IPingsModule pingsModule,
        CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteCommandAsync(new ThrowAfterTouchCommand(pingId), cancellationToken)
            .ToNoContentAsync();
}
