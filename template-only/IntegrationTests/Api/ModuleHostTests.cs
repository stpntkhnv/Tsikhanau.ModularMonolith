using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;
using Tsikhanau.Railway;

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
            [
                "Pings GET /pings/{pingId:guid}",
                "Pings POST /pings/",
                "Pings POST /pings/{pingId:guid}/touch",
                "Pings GET /pings/errors/{kind}",
                "Pings POST /pings/{pingId:guid}/throw",
                "Pongs GET /pongs/{pongId:guid}/source"
            ],
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

        var ping = await httpClient.GetFromJsonAsync<PingDto>(
            $"/pings/{pingId}",
            testModulesHost.HttpJsonSerializerOptions,
            CancellationToken);

        ping.ShouldBe(new PingDto(pingId, "over http", Optional<String>.None(), 0, 1));
    }

    [Fact]
    public async Task Touch_endpoint_answers_no_content_and_touches_the_ping()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("touched over http");
        using var httpClient = testModulesHost.CreateClient();

        var response = await httpClient.PostAsync($"/pings/{pingId}/touch", content: null, CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        var ping = await testModulesHost.GetPingAsync(pingId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        body.ShouldBeEmpty();
        ping.ShouldBe(new PingDto(pingId, "touched over http", Optional<String>.None(), 1, 2));
    }

    [Fact]
    public async Task Create_endpoint_answers_created_with_the_address_of_the_new_ping()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var createResponse = await httpClient.PostAsync(
            "/pings",
            JsonBody("""{"label":"created over http","nickname":"nick"}"""),
            CancellationToken);

        var createdPingId = await createResponse.Content.ReadAsStringAsync(CancellationToken);
        var createdPing = await ReadCreatedPingAsync(httpClient, createResponse);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        createdPing.ShouldBe(
            $$"""{"id":{{createdPingId}},"label":"created over http","nickname":"nick","touches":0,"version":1}""");
    }

    [Theory]
    [InlineData("""{"label":"without nickname"}""")]
    [InlineData("""{"label":"without nickname","nickname":null}""")]
    public async Task Create_endpoint_reads_a_missing_or_null_nickname_as_none(String body)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var createResponse = await httpClient.PostAsync("/pings", JsonBody(body), CancellationToken);
        var createdPingId = await createResponse.Content.ReadAsStringAsync(CancellationToken);
        var createdPing = await ReadCreatedPingAsync(httpClient, createResponse);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        createdPing.ShouldBe(
            $$"""{"id":{{createdPingId}},"label":"without nickname","nickname":null,"touches":0,"version":1}""");
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

    private static StringContent JsonBody(String json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<String> ReadCreatedPingAsync(HttpClient httpClient, HttpResponseMessage createResponse)
    {
        var pingResponse = await httpClient.GetAsync(createResponse.Headers.Location, CancellationToken);
        return await pingResponse.Content.ReadAsStringAsync(CancellationToken);
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
