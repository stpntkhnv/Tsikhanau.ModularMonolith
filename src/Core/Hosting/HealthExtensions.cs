using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Tsikhanau.Monolith.Hosting;

internal static class HealthExtensions
{
    private const String HealthEndpointPath = "/health";
    private const String AlivenessEndpointPath = "/alive";
    private const String LiveTag = "live";

    public static IHostApplicationBuilder AddHealth(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    public static WebApplication MapHealth(this WebApplication app)
    {
        app
            .MapHealthChecks(HealthEndpointPath)
            .AllowAnonymous();

        app
            .MapHealthChecks(
                AlivenessEndpointPath,
                new HealthCheckOptions { Predicate = registration => registration.Tags.Contains(LiveTag) })
            .AllowAnonymous();

        return app;
    }

    public static Boolean IsProbe(PathString path) =>
        path.StartsWithSegments(HealthEndpointPath) || path.StartsWithSegments(AlivenessEndpointPath);
}
