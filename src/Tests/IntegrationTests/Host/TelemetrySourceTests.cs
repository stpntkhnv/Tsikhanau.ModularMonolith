using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Testing.App;
using OpenTelemetry.Trace;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class TelemetrySourceTests(AppFixture appFixture) : AppTest(appFixture)
{
    [Fact]
    public void Host_traces_the_activity_source_of_the_core()
    {
        _ = App.Services.GetRequiredService<TracerProvider>();
        using var coreActivitySource = new ActivitySource("Tsikhanau.Monolith");

        coreActivitySource
            .HasListeners()
            .ShouldBeTrue();
    }
}
