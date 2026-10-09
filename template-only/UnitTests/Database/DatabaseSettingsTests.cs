using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Platform;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class DatabaseSettingsTests
{
    [Fact]
    public void Prepare_timeout_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Database:MaxConnections"] = "20",
                ["ModularMonolith:Database:PrepareTimeout"] = "00:05:00",
            });

        settings.PrepareTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Missing_prepare_timeout_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["ModularMonolith:Database:MaxConnections"] = "20" }));

    [Theory]
    [InlineData("00:00:09")]
    [InlineData("01:00:01")]
    public void Prepare_timeout_outside_ten_seconds_to_an_hour_is_rejected(String prepareTimeout) =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["ModularMonolith:Database:MaxConnections"] = "20",
                    ["ModularMonolith:Database:PrepareTimeout"] = prepareTimeout,
                }));

    [Theory]
    [InlineData(20, 2, 10)]
    [InlineData(21, 2, 10)]
    [InlineData(3, 2, 2)]
    public void Pool_size_splits_the_budget_evenly_and_never_drops_below_two_per_module(
        Int32 maxConnections,
        Int32 moduleCount,
        Int32 poolSizePerModule)
    {
        var settings = new DatabaseSettings
        {
            MaxConnections = maxConnections,
            PrepareTimeout = TimeSpan.FromMinutes(10),
        };

        var calculatedPoolSizePerModule = settings.CalculatePoolSizePerModule(moduleCount);

        calculatedPoolSizePerModule.ShouldBe(poolSizePerModule);
    }

    private static DatabaseSettings ResolveSettings(Dictionary<String, String?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new ServiceCollection()
            .AddPlatformServices(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<DatabaseSettings>>().Value;
    }
}
