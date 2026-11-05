using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Hosting;

namespace Tsikhanau.Monolith.UnitTests.ProbeApps;

internal static class ProbeApp
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static Task<WebApplication> StartAsync(Action<WebApplication> mapProbe) =>
        StartAsync(_ => { }, mapProbe);

    public static Task<WebApplication> StartAsync(
        Action<WebApplicationBuilder> configureApp,
        Action<WebApplication> mapProbe) =>
        StartAsync(Environments.Production, configureApp, mapProbe);

    public static Task<WebApplication> StartInDevelopmentAsync(Action<WebApplication> mapProbe) =>
        StartAsync(Environments.Development, _ => { }, mapProbe);

    private static async Task<WebApplication> StartAsync(
        String environmentName,
        Action<WebApplicationBuilder> configureApp,
        Action<WebApplication> mapProbe)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = environmentName });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();
        configureApp(builder);

        var app = builder.Build();
        app.UseServiceDefaults();
        mapProbe(app);
        await app.StartAsync(CancellationToken);
        return app;
    }
}
