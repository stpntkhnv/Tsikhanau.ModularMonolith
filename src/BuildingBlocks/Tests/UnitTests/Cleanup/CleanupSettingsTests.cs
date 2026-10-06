using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;

namespace ModularMonolith.BuildingBlocks.UnitTests.Cleanup;

public sealed class CleanupSettingsTests
{
    [Fact]
    public void Interval_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Cleanup:Interval"] = "00:30:00",
                ["ModularMonolith:Cleanup:KeepFor"] = "7.00:00:00",
            });

        settings.Interval.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Keep_for_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Cleanup:Interval"] = "00:30:00",
                ["ModularMonolith:Cleanup:KeepFor"] = "3.00:00:00",
            });

        settings.KeepFor.ShouldBe(TimeSpan.FromDays(3));
    }

    [Fact]
    public void Missing_keep_for_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["ModularMonolith:Cleanup:Interval"] = "00:30:00" }));

    [Fact]
    public void Missing_interval_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["ModularMonolith:Cleanup:KeepFor"] = "7.00:00:00" }));

    private static CleanupSettings ResolveSettings(Dictionary<String, String?> configurationValues)
    {
        var cleanupValues = new Dictionary<String, String?>(configurationValues)
        {
            ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
            ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
            ["ModularMonolith:Queues:ClaimBatchSize"] = "16",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(cleanupValues)
            .Build();

        return new ServiceCollection()
            .AddPlatformServices(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<CleanupSettings>>().Value;
    }
}
