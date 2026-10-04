using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ModularMonolith.Bootstrapper.Defaults;

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
