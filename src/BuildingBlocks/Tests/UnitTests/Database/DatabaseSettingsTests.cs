using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;

namespace ModularMonolith.BuildingBlocks.UnitTests.Database;

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
