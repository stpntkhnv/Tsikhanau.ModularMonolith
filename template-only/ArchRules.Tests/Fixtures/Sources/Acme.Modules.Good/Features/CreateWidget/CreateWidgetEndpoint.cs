using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed class CreateWidgetEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/", CreateAsync);

    private static async Task<IResult> CreateAsync(
        CreateWidgetRequest request,
        IGoodModule goodModule,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var widgetId = request.WidgetId.GetValueOrDefault(() => Guid.CreateVersion7(timeProvider.GetUtcNow()));
        var created = await goodModule.ExecuteCommandAsync(
            new CreateWidgetCommand(widgetId, request.Label, request.Price),
            cancellationToken);

        return created.IsSuccess ? Results.Ok(created.Value) : Results.BadRequest();
    }
}
