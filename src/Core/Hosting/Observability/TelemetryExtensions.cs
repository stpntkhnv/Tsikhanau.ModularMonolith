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
using Tsikhanau.Monolith.Infrastructure.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.Hosting.Observability;

internal static class TelemetryExtensions
{
    private const String OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const String NpgsqlTelemetry = "Npgsql";

    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder, String[] activitySources)
    {
        if (builder.Services.TryAddMarker<TelemetryMarker>())
        {
            builder.AddLogsMetricsAndTraces();
        }

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(activitySources));

        return builder;
    }

    public static IHostApplicationBuilder AddOtlpExporterWhenConfigured(this IHostApplicationBuilder builder)
    {
        if (HasOtlpEndpoint(builder.Configuration) && builder.Services.TryAddMarker<OtlpExporterMarker>())
        {
            builder.Services
                .AddOpenTelemetry()
                .UseOtlpExporter();
        }

        return builder;
    }

    private static void AddLogsMetricsAndTraces(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(IncludeMessagesAndScopes);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => DescribeService(resourceBuilder, builder.Environment.ApplicationName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(NpgsqlTelemetry)
                .AddMeter(AppDiagnostics.SourceName))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(AppDiagnostics.SourceName)
                .AddSource(NpgsqlTelemetry)
                .AddAspNetCoreInstrumentation(SkipProbes)
                .AddHttpClientInstrumentation());
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

file sealed class TelemetryMarker;

file sealed class OtlpExporterMarker;
