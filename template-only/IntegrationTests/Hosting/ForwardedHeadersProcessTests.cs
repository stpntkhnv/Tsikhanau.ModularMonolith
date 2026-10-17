using System.Net;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

public sealed class ForwardedHeadersProcessTests(PostgresFixture postgresFixture)
{
    private const String EchoPath = "/probe/request";

    private const String ForwardedProto = "https";

    private const String ForwardedFor = "203.0.113.7";

    private const String Loopback = "127.0.0.1";

    private const String Applied = $"scheme={ForwardedProto} remote={ForwardedFor}";

    private const String Ignored = $"scheme=http remote={Loopback}";

    private const String OtherProxy = "192.0.2.1";

    private const String OtherNetwork = "192.0.2.0/24";

    private const String LoopbackNetwork = "127.0.0.0/8";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", "", "", Ignored)]
    [InlineData("true", "", "", Applied)]
    [InlineData("true", OtherProxy, "", Ignored)]
    [InlineData("true", Loopback, "", Applied)]
    [InlineData("true", "", OtherNetwork, Ignored)]
    [InlineData("true", "", LoopbackNetwork, Applied)]
    public async Task Forwarded_headers_apply_only_when_the_framework_switch_is_on_and_the_caller_is_trusted(
        String enabled,
        String knownProxy,
        String knownNetwork,
        String expected)
    {
        var environment = await CreateEnvironmentAsync();
        environment["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] = enabled;
        environment["TestHost__EchoRequest"] = "true";
        environment["TestHost__KnownProxy"] = knownProxy;
        environment["TestHost__KnownNetwork"] = knownNetwork;
        var url = environment[TestHostEnvironment.UrlsKey];

        using var testHost = TestHostProcess.Start(environment);
        await testHost.WaitUntilHealthyAsync(url);
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}{EchoPath}");
        request.Headers.Add("X-Forwarded-Proto", ForwardedProto);
        request.Headers.Add("X-Forwarded-For", ForwardedFor);
        using var response = await httpClient.SendAsync(request, CancellationToken);
        var echo = await response.Content.ReadAsStringAsync(CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        echo.ShouldBe(expected);
    }

    private async Task<Dictionary<String, String>> CreateEnvironmentAsync()
    {
        var connectionString = await TestHostEnvironment.CreateDatabaseAsync(
            postgresFixture.ConnectionString,
            "forwarded_headers");

        return TestHostEnvironment.Create(connectionString);
    }
}
