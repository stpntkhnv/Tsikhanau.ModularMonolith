using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Contracts.Secrets;
using ModularMonolith.BuildingBlocks.Infrastructure.EventBus;

namespace ModularMonolith.BuildingBlocks.UnitTests.EventBus;

public sealed class IntegrationEventRegistryTests
{
    [Fact]
    public void Registered_event_resolves_by_its_stable_name()
    {
        var integrationEventRegistry = new IntegrationEventRegistry();
        integrationEventRegistry.Register(typeof(FirstEvent));

        var name = integrationEventRegistry.GetName(typeof(FirstEvent));
        var knownType = integrationEventRegistry.FindType("tests.first");
        var unknownType = integrationEventRegistry.FindType("tests.unknown");

        name.ShouldBe("tests.first");
        knownType.Value.ShouldBe(typeof(FirstEvent));
        unknownType.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void Two_types_with_one_name_stop_registration()
    {
        var integrationEventRegistry = new IntegrationEventRegistry();
        integrationEventRegistry.Register(typeof(FirstEvent));

        var exception = Should.Throw<InvalidOperationException>(
            () => integrationEventRegistry.Register(typeof(ClashingEvent)));

        exception.Message.ShouldContain("tests.first");
    }

    [Fact]
    public void Event_without_a_name_is_rejected() =>
        Should.Throw<InvalidOperationException>(() => new IntegrationEventRegistry().GetName(typeof(NamelessEvent)));

    [Fact]
    public void Registry_knows_which_events_carry_secrets()
    {
        var integrationEventRegistry = new IntegrationEventRegistry();
        integrationEventRegistry.Register(typeof(SecretTestIntegrationEvent));
        integrationEventRegistry.Register(typeof(PlainTestIntegrationEvent));

        var secretEventContainsSecrets = integrationEventRegistry.ContainsSecrets("tests.secret");
        var plainEventContainsSecrets = integrationEventRegistry.ContainsSecrets("tests.plain");
        var unknownEventContainsSecrets = integrationEventRegistry.ContainsSecrets("tests.unknown");

        secretEventContainsSecrets.ShouldBeTrue();
        plainEventContainsSecrets.ShouldBeFalse();
        unknownEventContainsSecrets.ShouldBeFalse();
    }

    [IntegrationEventName("tests.secret")]
    [ContainsSecrets]
    private sealed record SecretTestIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);

    [IntegrationEventName("tests.plain")]
    private sealed record PlainTestIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);

    [IntegrationEventName("tests.first")]
    private sealed record FirstEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);

    [IntegrationEventName("tests.first")]
    private sealed record ClashingEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);

    private sealed record NamelessEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
}
