using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Testing.App;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Host;

public abstract class TelemetrySourceTests<TEntryPoint, TAppFixture>(TAppFixture appFixture)
    : AppTest<TEntryPoint, TAppFixture>(appFixture)
    where TEntryPoint : class
    where TAppFixture : AppFixture<TEntryPoint>
{
    [Fact]
    public void Activity_source_of_the_core_has_the_fixed_name() =>
        AppDiagnostics.ActivitySource.Name.ShouldBe("Tsikhanau.Monolith");

    [Fact]
    public void Host_traces_the_activity_source_of_the_core()
    {
        _ = App.Services.GetRequiredService<TracerProvider>();

        AppDiagnostics.ActivitySource
            .HasListeners()
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Host_with_an_exporter_collects_the_meter_of_the_core()
    {
        await using var appFactory = App.StartFactory(
            Environments.Development,
            new Dictionary<String, String> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1" });

        _ = appFactory.Services.GetRequiredService<MeterProvider>();

        var counter = appFactory.Services
            .GetRequiredService<IMeterFactory>()
            .Create(AppDiagnostics.SourceName)
            .CreateCounter<Int64>("tsikhanau.telemetry.probe");

        counter.Enabled.ShouldBeTrue();
    }
}
