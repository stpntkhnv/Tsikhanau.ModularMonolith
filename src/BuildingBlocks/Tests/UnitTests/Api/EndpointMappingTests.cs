using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Endpoints;

namespace ModularMonolith.BuildingBlocks.UnitTests.Api;

public sealed class EndpointMappingTests
{
    [Fact]
    public async Task Maps_every_concrete_endpoint_in_the_assembly()
    {
        await using var app = WebApplication
            .CreateSlimBuilder()
            .Build();

        app.MapEndpointsFrom(typeof(EndpointMappingTests).Assembly);

        var routes = GetRoutes(app);

        routes.ShouldBe(["/sample"]);
    }

    private static IEnumerable<String?> GetRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);

    private sealed class SampleEndpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/sample", () => "sample");
    }

    private abstract class AbstractEndpoint : IEndpoint
    {
        public abstract void Map(IEndpointRouteBuilder endpoints);
    }
}
