using System.Text.Json;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.BuildingBlocks.UnitTests.EventBus;

public sealed class IntegrationEventIdTests
{
    private static readonly DateTimeOffset OccurredOn = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Two_events_from_the_same_moment_get_different_ids()
    {
        var firstEvent = new SampleIntegrationEvent(OccurredOn, "fact");
        var secondEvent = new SampleIntegrationEvent(OccurredOn, "fact");

        firstEvent.Id.ShouldNotBe(secondEvent.Id);
        firstEvent.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Json_round_trip_keeps_the_id()
    {
        var original = new SampleIntegrationEvent(OccurredOn, "fact");
        var json = JsonSerializer.Serialize(original, WebJsonSerializerOptions);

        var copy = JsonSerializer.Deserialize<SampleIntegrationEvent>(json, WebJsonSerializerOptions);

        copy.ShouldBe(original);
    }

    [Fact]
    public void Payload_without_an_id_is_refused()
    {
        const String json = """{"occurredOn":"2026-10-02T08:00:00+00:00","fact":"fact"}""";

        Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<SampleIntegrationEvent>(json, WebJsonSerializerOptions));
    }

    private sealed record SampleIntegrationEvent(DateTimeOffset OccurredOn, String Fact)
        : IntegrationEvent(OccurredOn);
}
