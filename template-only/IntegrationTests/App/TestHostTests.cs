using System.Net;
using System.Text;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class TestHostTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private const String Label = "through the test host";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Fixture_starts_the_test_host_with_both_test_modules()
    {
        var pingsModule = App.GetModule("Pings");
        var pongsModule = App.GetModule("Pongs");

        pingsModule.Schema.ShouldBe("pings");
        pongsModule.Schema.ShouldBe("pongs");
    }

    [Fact]
    public async Task Client_of_the_fixture_reaches_the_pings_endpoints()
    {
        using var httpClient = App.CreateClient();

        var createResponse = await httpClient.PostAsync(
            "/pings",
            JsonBody($$"""{"label":"{{Label}}"}"""),
            CancellationToken);

        var createdPingId = await createResponse.Content.ReadAsStringAsync(CancellationToken);
        var pingResponse = await httpClient.GetAsync(createResponse.Headers.Location, CancellationToken);
        var ping = await pingResponse.Content.ReadAsStringAsync(CancellationToken);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createdPingId);
        ping.ShouldBe($$"""{"id":{{createdPingId}},"label":"{{Label}}","nickname":null,"touches":0,"version":1}""");
    }

    private static StringContent JsonBody(String json) => new(json, Encoding.UTF8, "application/json");
}
