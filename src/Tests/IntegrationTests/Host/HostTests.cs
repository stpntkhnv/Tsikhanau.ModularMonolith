using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.Testing.App;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class HostTests(AppFixture appFixture) : AppTest(appFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_is_healthy()
    {
        using var httpClient = App.CreateClient();

        var response = await httpClient.GetAsync("/health", CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_endpoints_are_served_outside_development()
    {
        await using var productionAppFactory = App.StartFactory(Environments.Production);
        using var httpClient = productionAppFactory.CreateClient();

        var hostEnvironment = productionAppFactory.Services.GetRequiredService<IHostEnvironment>();
        var healthResponse = await httpClient.GetAsync("/health", CancellationToken);
        var aliveResponse = await httpClient.GetAsync("/alive", CancellationToken);

        hostEnvironment.EnvironmentName.ShouldBe(Environments.Production);
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        aliveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var httpClient = App.CreateClient();

        var response = await httpClient.GetAsync("/openapi/v1.json", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_route_answers_not_found_with_problem_details()
    {
        using var httpClient = App.CreateClient();

        var response = await httpClient.GetAsync("/nowhere", CancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        mediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public void Missing_connection_string_stops_the_host_at_startup()
    {
        using var webApplicationFactory = new WebApplicationFactory<Program>();

        var exception = Should.Throw<Exception>(() => webApplicationFactory.Services);
        var details = exception.ToString();

        details.ShouldContain("Connection string 'modularmonolith' is missing");
    }

    [Fact]
    public void Connection_string_without_a_password_stops_the_host_at_startup()
    {
        var variableName = $"ConnectionStrings__{PlatformServiceCollectionExtensions.ConnectionName}";
        var connectionString = "Host=localhost;Database=modularmonolith;Username=postgres";
        using var environmentOverride = new EnvironmentOverride(
            new Dictionary<String, String> { [variableName] = connectionString });

        using var webApplicationFactory = new WebApplicationFactory<Program>();

        var exception = Should.Throw<Exception>(() => webApplicationFactory.Services);
        var details = exception.ToString();

        details.ShouldContain("has no password");
    }
}
