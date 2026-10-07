using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.UnitTests.Api.Scenarios.Owner;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api;

public sealed class EndpointMappingTests
{
    [Fact]
    public async Task Maps_only_the_concrete_endpoints_in_the_namespace_of_the_module_class()
    {
        await using var app = WebApplication
            .CreateSlimBuilder()
            .Build();

        app.MapEndpointsOf<OwnerModule>();

        var routes = GetRoutes(app);

        routes.ShouldBe(["/owner"]);
    }

    private static IEnumerable<String?> GetRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);
}
