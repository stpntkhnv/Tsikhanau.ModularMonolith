using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.UnitTests.Hosting;

public sealed class ServiceDefaultsTests
{
    private const String OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const String ClosedOtlpEndpoint = "http://127.0.0.1:1";

    private const String SelfCheckName = "self";

    private const String FirstSourceName = "Tsikhanau.Monolith.UnitTests.FirstSource";

    private const String SecondSourceName = "Tsikhanau.Monolith.UnitTests.SecondSource";

    [Fact]
    public async Task Repeated_service_defaults_register_one_self_check_and_one_exporter()
    {
        var builder = CreateBuilderWithOtlpEndpoint();
        builder.AddServiceDefaults(AppDiagnostics.SourceName);
        builder.AddServiceDefaults(AppDiagnostics.SourceName);
        await using var app = builder.Build();

        var selfCheckCount = CountSelfChecks(app.Services);
        var isCoreMeterExported = IsCoreMeterExported(app.Services);

        selfCheckCount.ShouldBe(1);
        isCoreMeterExported.ShouldBeTrue();
    }

    [Fact]
    public async Task Host_that_calls_only_the_service_defaults_exports_to_the_configured_endpoint()
    {
        var builder = CreateBuilderWithOtlpEndpoint();
        builder.AddServiceDefaults(AppDiagnostics.SourceName);
        await using var app = builder.Build();

        var isCoreMeterExported = IsCoreMeterExported(app.Services);

        isCoreMeterExported.ShouldBeTrue();
    }

    [Fact]
    public async Task Repeated_service_defaults_trace_the_sources_of_every_call()
    {
        using var firstSource = new ActivitySource(FirstSourceName);
        using var secondSource = new ActivitySource(SecondSourceName);
        var builder = CreateBuilder();
        builder.AddServiceDefaults(FirstSourceName);
        builder.AddServiceDefaults(SecondSourceName);
        await using var app = builder.Build();
        _ = app.Services.GetRequiredService<TracerProvider>();

        IReadOnlyList<ActivitySource> sources = [firstSource, secondSource];
        var tracedSourceNames = sources
            .Where(source => source.HasListeners())
            .Select(source => source.Name)
            .ToList();

        tracedSourceNames.ShouldBe([FirstSourceName, SecondSourceName]);
    }

    [Fact]
    public async Task Service_defaults_trace_the_activity_source_of_the_core_without_being_given_it()
    {
        var builder = CreateBuilder();
        builder.AddServiceDefaults();
        await using var app = builder.Build();
        _ = app.Services.GetRequiredService<TracerProvider>();

        var isCoreSourceTraced = AppDiagnostics.ActivitySource.HasListeners();

        isCoreSourceTraced.ShouldBeTrue();
    }

    private static WebApplicationBuilder CreateBuilderWithOtlpEndpoint()
    {
        var builder = CreateBuilder();
        builder.Configuration[OtlpEndpointKey] = ClosedOtlpEndpoint;
        return builder;
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.Logging.ClearProviders();
        return builder;
    }

    private static Int32 CountSelfChecks(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Count(registration => registration.Name == SelfCheckName);

    private static Boolean IsCoreMeterExported(IServiceProvider serviceProvider)
    {
        _ = serviceProvider.GetRequiredService<MeterProvider>();

        var probeCounter = serviceProvider
            .GetRequiredService<IMeterFactory>()
            .Create(AppDiagnostics.SourceName)
            .CreateCounter<Int64>("tsikhanau.telemetry.probe");

        return probeCounter.Enabled;
    }
}
