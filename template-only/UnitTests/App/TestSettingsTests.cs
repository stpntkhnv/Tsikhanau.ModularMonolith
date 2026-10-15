using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.UnitTests.App;

public sealed class TestSettingsTests
{
    [Fact]
    public void Module_settings_come_after_the_core_settings_and_overrides_after_both()
    {
        var environmentVariables = TestSettings.CreateEnvironmentVariables(
            "Host=database",
            [
                new("App:Queues:RunInBackground", "true"),
                new("App:Modules:Pings:Limit", "10"),
                new("App:Modules:Pongs:Limit", "10"),
            ],
            new Dictionary<String, String>
            {
                ["App__Modules__Pings__Limit"] = "20",
                ["App:Modules:Pongs:Limit"] = "30",
                ["OTEL_SERVICE_NAME"] = "orders",
            });

        environmentVariables.ShouldBe(
            new Dictionary<String, String>
            {
                ["ConnectionStrings__database"] = "Host=database",
                ["App__Database__LoginKey"] = TestSettings.LoginKey,
                ["App__Queues__RunInBackground"] = "true",
                ["App__Cleanup__RunInBackground"] = "false",
                ["App__Modules__Pings__Limit"] = "20",
                ["App__Modules__Pongs__Limit"] = "30",
                ["OTEL_SERVICE_NAME"] = "orders",
            },
            ignoreOrder: true);
    }
}
