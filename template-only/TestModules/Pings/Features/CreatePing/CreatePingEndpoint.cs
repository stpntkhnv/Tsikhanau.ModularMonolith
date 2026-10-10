using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;

internal sealed class CreatePingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(String.Empty, CreateAsync)
            .WithName("Pings.CreatePing");

    private static Task<Results<CreatedAtRoute<Guid>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        CreatePingRequest request,
        IPingsModule pingsModule,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteCommandAsync(CreateCommand(request, timeProvider), cancellationToken)
            .ToCreatedAsync("Pings.GetPing", pingId => new { pingId });

    private static CreatePingCommand CreateCommand(CreatePingRequest request, TimeProvider timeProvider) =>
        new(Guid.CreateVersion7(timeProvider.GetUtcNow()), request.Label, request.Nickname);
}
