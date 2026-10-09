using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Platform;

namespace Tsikhanau.Monolith.UnitTests.Cleanup;

public sealed class CleanupSettingsTests
{
    [Fact]
    public void Interval_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Cleanup:Interval"] = "00:30:00",
                ["App:Cleanup:KeepFor"] = "7.00:00:00",
            });

        settings.Interval.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Keep_for_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Cleanup:Interval"] = "00:30:00",
                ["App:Cleanup:KeepFor"] = "3.00:00:00",
            });

        settings.KeepFor.ShouldBe(TimeSpan.FromDays(3));
    }

    [Fact]
    public void Missing_keep_for_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["App:Cleanup:Interval"] = "00:30:00" }));

    [Fact]
    public void Missing_interval_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["App:Cleanup:KeepFor"] = "7.00:00:00" }));

    private static CleanupSettings ResolveSettings(Dictionary<String, String?> configurationValues)
    {
        var cleanupValues = new Dictionary<String, String?>(configurationValues)
        {
            ["App:Queues:PollInterval"] = "00:00:03",
            ["App:Queues:HandlerTimeout"] = "00:01:00",
            ["App:Queues:ClaimBatchSize"] = "16",
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
