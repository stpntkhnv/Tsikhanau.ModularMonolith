using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Testing.App;
using OpenTelemetry.Trace;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class TelemetrySourceTests(AppFixture appFixture) : AppTest(appFixture)
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
}
