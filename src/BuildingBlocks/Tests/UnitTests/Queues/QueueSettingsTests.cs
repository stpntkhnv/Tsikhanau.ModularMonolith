using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.UnitTests.Queues;

public sealed class QueueSettingsTests
{
    [Fact]
    public void Poll_interval_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
            });

        settings.PollInterval.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Handler_timeout_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                ["ModularMonolith:Queues:HandlerTimeout"] = "00:00:30",
            });

        settings.HandlerTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Missing_handler_timeout_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["ModularMonolith:Queues:PollInterval"] = "00:00:03" }));

    [Fact]
    public void Missing_poll_interval_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(new() { ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00" }));

    private static QueueSettings ResolveSettings(Dictionary<String, String?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new ServiceCollection()
            .AddPlatformServices(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<QueueSettings>>().Value;
    }
}
