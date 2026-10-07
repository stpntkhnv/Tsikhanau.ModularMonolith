using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Testing.App;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class TelemetryResourceTests(AppFixture appFixture) : AppTest(appFixture)
{
    [Fact]
    public void Telemetry_resource_names_the_application_and_the_machine()
    {
        var attributes = ReadResourceAttributes(App.Services);

        attributes["service.name"].ShouldBe("ModularMonolith.Bootstrapper");
        attributes["service.instance.id"].ShouldBe(Environment.MachineName);
    }

    [Fact]
    public async Task Otel_environment_variables_override_the_telemetry_resource()
    {
        await using var appFactory = App.StartFactory(
            Environments.Development,
            new Dictionary<String, String>
            {
                ["OTEL_SERVICE_NAME"] = "orders",
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.instance.id=orders-1",
            });

        var attributes = ReadResourceAttributes(appFactory.Services);

        attributes["service.name"].ShouldBe("orders");
        attributes["service.instance.id"].ShouldBe("orders-1");
    }

    private static Dictionary<String, Object> ReadResourceAttributes(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<TracerProvider>()
            .GetResource().Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
}
