using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.UnitTests.Api.Scenarios.InventoryReservations;
using Tsikhanau.Monolith.UnitTests.Api.Scenarios.Owner;

namespace Tsikhanau.Monolith.UnitTests.Api;

public sealed class EndpointMappingTests
{
    [Fact]
    public async Task Maps_only_the_concrete_endpoints_in_the_namespace_of_the_module_class()
    {
        await using var app = WebApplication
            .CreateSlimBuilder()
            .Build();

        app
            .MapGroup(String.Empty)
            .MapEndpointsOf<OwnerModule>();

        var routes = GetRouteEndpoints(app)
            .Select(endpoint => endpoint.RoutePattern.RawText);

        routes.ShouldBe(["/owner"]);
    }

    [Fact]
    public async Task Describes_the_error_responses_on_the_group_for_every_endpoint()
    {
        await using var app = WebApplication
            .CreateSlimBuilder()
            .Build();

        app
            .MapGroup(String.Empty)
            .MapEndpointsOf<OwnerModule>();

        var errorResponses = GetRouteEndpoints(app)
            .Single().Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(response => response.StatusCode >= StatusCodes.Status400BadRequest)
            .Select(DescribeResponse);

        errorResponses.ShouldBe(
        [
            "400 application/problem+json HttpValidationProblemDetails",
            "401 application/problem+json ProblemDetails",
            "403 application/problem+json ProblemDetails",
            "404 application/problem+json ProblemDetails",
            "409 application/problem+json ProblemDetails",
            "422 application/problem+json ProblemDetails",
            "500 application/problem+json ProblemDetails"
        ]);
    }

    [Fact]
    public async Task A_module_maps_its_endpoints_in_a_kebab_case_group_tagged_with_its_name()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.AddModule<InventoryReservationsModule>();
        await using var app = builder.Build();
        app.MapModules();

        var endpoint = GetRouteEndpoints(app)
            .Single();

        endpoint.RoutePattern.RawText.ShouldBe("/inventory-reservations/{reservationId:guid}");
        endpoint.Metadata
            .GetRequiredMetadata<ITagsMetadata>().Tags
            .ShouldBe(["InventoryReservations"]);
    }

    private static IEnumerable<RouteEndpoint> GetRouteEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>();

    private static String DescribeResponse(IProducesResponseTypeMetadata response) =>
        $"{response.StatusCode} {String.Join(", ", response.ContentTypes)} {response.Type?.Name}";
}
