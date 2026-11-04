using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Shouldly;
using Tsikhanau.Monolith.Testing.App;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Host;

public abstract class TelemetryResourceTests<TEntryPoint, TAppFixture>(TAppFixture appFixture)
    : AppTest<TEntryPoint, TAppFixture>(appFixture)
    where TEntryPoint : class
    where TAppFixture : AppFixture<TEntryPoint>
{
    [Fact]
    public void Telemetry_resource_names_the_application_and_the_machine()
    {
        var attributes = ReadResourceAttributes(App.Services);

        attributes["service.name"].ShouldBe(typeof(TEntryPoint).Assembly.GetName().Name);
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
