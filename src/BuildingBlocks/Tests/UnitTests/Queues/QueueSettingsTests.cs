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
                ["ModularMonolith:Queues:ClaimBatchSize"] = "16",
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
                ["ModularMonolith:Queues:ClaimBatchSize"] = "16",
            });

        settings.HandlerTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Claim_batch_size_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
                ["ModularMonolith:Queues:ClaimBatchSize"] = "7",
            });

        settings.ClaimBatchSize.ShouldBe(7);
    }

    [Fact]
    public void Missing_handler_timeout_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                    ["ModularMonolith:Queues:ClaimBatchSize"] = "16",
                }));

    [Fact]
    public void Missing_poll_interval_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
                    ["ModularMonolith:Queues:ClaimBatchSize"] = "16",
                }));

    [Fact]
    public void Missing_claim_batch_size_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                    ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
                }));

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    public void Claim_batch_size_outside_one_to_a_hundred_is_rejected(String claimBatchSize) =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["ModularMonolith:Queues:PollInterval"] = "00:00:03",
                    ["ModularMonolith:Queues:HandlerTimeout"] = "00:01:00",
                    ["ModularMonolith:Queues:ClaimBatchSize"] = claimBatchSize,
                }));

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
