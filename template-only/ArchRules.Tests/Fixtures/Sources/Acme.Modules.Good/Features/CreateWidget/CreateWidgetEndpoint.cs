using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed class CreateWidgetEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/", CreateAsync);

    private static Task<Results<Ok<Guid>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        CreateWidgetRequest request,
        IGoodModule goodModule,
        CancellationToken cancellationToken) =>
        goodModule
            .ExecuteCommandAsync(
                new CreateWidgetCommand(request.WidgetId, request.Label, request.Price),
                cancellationToken)
            .ToOkAsync();
}
