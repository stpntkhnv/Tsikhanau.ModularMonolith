using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Testing.Queues;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class IntegrationEventPublisherTests
{
    private static readonly DateTimeOffset OccurredOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Publishing_an_event_no_module_subscribes_to_is_refused_before_its_payload_is_serialized()
    {
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton<IntegrationEventRegistry>()
            .AddSingleton<SubscriptionRegistry>()
            .BuildServiceProvider();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => IntegrationEventPublisher.PublishAsync(
                serviceProvider,
                new UnserializableIntegrationEvent(OccurredOn)));

        exception.Message.ShouldBe(
            "No module subscribes to tests.unserializable "
            + $"({typeof(UnserializableIntegrationEvent).FullName}); "
            + "a module subscribes with an IIntegrationEventHandler<UnserializableIntegrationEvent>");
    }

    [IntegrationEventName("tests.unserializable")]
    private sealed record UnserializableIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn)
    {
        public Type PayloadType => typeof(String);
    }
}
