using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Tsikhanau.Monolith.Hosting;

internal static class TelemetryExtensions
{
    private const String OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const String NpgsqlTelemetry = "Npgsql";

    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder, String[] activitySources)
    {
        builder.Logging.AddOpenTelemetry(IncludeMessagesAndScopes);

        var openTelemetryBuilder = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => DescribeService(resourceBuilder, builder.Environment.ApplicationName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(NpgsqlTelemetry))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(activitySources)
                .AddSource(NpgsqlTelemetry)
                .AddAspNetCoreInstrumentation(SkipProbes)
                .AddHttpClientInstrumentation());

        if (HasOtlpEndpoint(builder.Configuration))
        {
            openTelemetryBuilder.UseOtlpExporter();
        }

        return builder;
    }

    private static void DescribeService(ResourceBuilder resourceBuilder, String applicationName) =>
        resourceBuilder
            .Clear()
            .AddService(
                applicationName,
                autoGenerateServiceInstanceId: false,
                serviceInstanceId: Environment.MachineName)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

    private static void IncludeMessagesAndScopes(OpenTelemetryLoggerOptions loggerOptions)
    {
        loggerOptions.IncludeFormattedMessage = true;
        loggerOptions.IncludeScopes = true;
    }

    private static void SkipProbes(AspNetCoreTraceInstrumentationOptions instrumentationOptions) =>
        instrumentationOptions.Filter = httpContext => !HealthExtensions.IsProbe(httpContext.Request.Path);

    private static Boolean HasOtlpEndpoint(IConfiguration configuration) =>
        !String.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
}
