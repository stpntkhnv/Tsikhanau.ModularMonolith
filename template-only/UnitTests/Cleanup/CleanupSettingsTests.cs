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
    public void Failed_rows_are_kept_for_thirty_days_when_the_setting_is_absent()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Cleanup:Interval"] = "00:30:00",
                ["App:Cleanup:KeepFor"] = "7.00:00:00",
            });

        settings.KeepFailedFor.ShouldBe(TimeSpan.FromDays(30));
    }

    [Fact]
    public void Keep_failed_for_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Cleanup:Interval"] = "00:30:00",
                ["App:Cleanup:KeepFor"] = "7.00:00:00",
                ["App:Cleanup:KeepFailedFor"] = "90.00:00:00",
            });

        settings.KeepFailedFor.ShouldBe(TimeSpan.FromDays(90));
    }

    [Theory]
    [InlineData("23:59:59")]
    [InlineData("365.00:00:01")]
    public void Keep_failed_for_outside_one_to_365_days_is_rejected_naming_the_setting(String keepFailedFor)
    {
        var exception = Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["App:Cleanup:Interval"] = "00:30:00",
                    ["App:Cleanup:KeepFor"] = "7.00:00:00",
                    ["App:Cleanup:KeepFailedFor"] = keepFailedFor,
                }));

        exception.Message.ShouldContain("KeepFailedFor");
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
