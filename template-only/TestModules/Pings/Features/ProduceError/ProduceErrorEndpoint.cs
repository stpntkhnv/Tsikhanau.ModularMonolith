using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

internal sealed class ProduceErrorEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet("/errors/{kind}", ProduceAsync)
            .WithName("Pings.ProduceError");

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ProduceAsync(
        ProducedErrorKind kind,
        IPingsModule pingsModule,
        CancellationToken cancellationToken) =>
        pingsModule
            .ExecuteQueryAsync(new ProduceErrorQuery(kind), cancellationToken)
            .ToNoContentAsync();
}
