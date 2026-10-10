using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class ModuleHostTests(PostgresFixture postgresFixture)
{
    private const String InvalidPongsSettingMessage =
        "DataAnnotation validation failed for 'PongsSettings' members: 'Limit' with the error: "
        + "'The field Limit must be between 1 and 100.'.";

    private static readonly String[] CoreHostRoutePrefixes = ["/health", "/alive", "/openapi/", "/scalar/"];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_module_maps_only_its_own_routes_under_its_group()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var routeEndpoints = ReadRouteEndpoints(testModulesHost);
        var moduleRoutes = routeEndpoints
            .Where(IsModuleRoute)
            .Select(DescribeModuleRoute);

        var hostRoutes = routeEndpoints
            .Where(IsHostRoute)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? String.Empty);

        moduleRoutes.ShouldBe(
            ["Pings GET /pings/{pingId:guid}", "Pongs GET /pongs/{pongId:guid}/source"],
            ignoreOrder: true);

        hostRoutes.ShouldAllBe(route => IsCoreHostRoute(route));
    }

    [Fact]
    public async Task Host_serves_health_alive_and_openapi_from_the_core_defaults()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var healthResponse = await httpClient.GetAsync("/health", CancellationToken);
        var aliveResponse = await httpClient.GetAsync("/alive", CancellationToken);
        var openApiResponse = await httpClient.GetAsync("/openapi/v1.json", CancellationToken);

        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        aliveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        openApiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pings_endpoint_answers_over_http()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("over http");
        using var httpClient = testModulesHost.CreateClient();

        var ping = await httpClient.GetFromJsonAsync<PingDto>($"/pings/{pingId}", CancellationToken);

        ping.ShouldBe(new PingDto(pingId, "over http", 0, 1));
    }

    [Fact]
    public async Task Pongs_endpoint_answers_over_http()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("source");
        await testModulesHost.ProcessQueuesAsync();
        using var httpClient = testModulesHost.CreateClient();

        var pongSource = await httpClient.GetFromJsonAsync<PongSourceDto>($"/pongs/{pingId}/source", CancellationToken);

        pongSource.ShouldBe(new PongSourceDto(pingId, "source"));
    }

    [Fact]
    public async Task Invalid_module_setting_stops_the_host_at_startup()
    {
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => TestModulesHost.StartAsync(
                postgresFixture,
                configure: builder => builder.Configuration["App:Modules:Pongs:Limit"] = "0"));

        exception.Message.ShouldBe(InvalidPongsSettingMessage);
    }

    private static IReadOnlyList<RouteEndpoint> ReadRouteEndpoints(TestModulesHost testModulesHost) =>
        testModulesHost.Services
            .GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

    private static Boolean IsModuleRoute(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ITagsMetadata>() is not null;

    private static Boolean IsHostRoute(RouteEndpoint endpoint) => !IsModuleRoute(endpoint);

    private static Boolean IsCoreHostRoute(String route) =>
        CoreHostRoutePrefixes.Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal));

    private static String DescribeModuleRoute(RouteEndpoint endpoint)
    {
        var module = endpoint.Metadata
            .GetRequiredMetadata<ITagsMetadata>().Tags
            .Single();

        var method = endpoint.Metadata
            .GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods
            .Single();

        return $"{module} {method} {endpoint.RoutePattern.RawText}";
    }
}
