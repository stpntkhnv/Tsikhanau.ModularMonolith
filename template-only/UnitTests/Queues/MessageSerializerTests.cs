using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class MessageSerializerTests
{
    private const String SomePayload =
        """{"label":"label","text":"text","number":5,"inner":{"name":"name","size":2}}""";

    private const String NullPayload = """{"label":"label","text":null,"number":null,"inner":null}""";

    private const String OlderPayload = """{"label":"label"}""";

    private static readonly PositionalMessage SomePositional = new("label", "text", 5, new SampleRecord("name", 2));

    private static readonly PositionalMessage NonePositional = new(
        "label",
        Optional<String>.None(),
        Optional<Int32>.None(),
        Optional<SampleRecord>.None());

    private static readonly InitMessage SomeInit = new()
    {
        Label = "label",
        Text = "text",
        Number = 5,
        Inner = new SampleRecord("name", 2),
    };

    private static readonly InitMessage NoneInit = new() { Label = "label" };

    [Fact]
    public void Some_is_written_as_its_value()
    {
        var positionalPayload = MessageSerializer.Serialize(SomePositional);
        var initPayload = MessageSerializer.Serialize(SomeInit);

        positionalPayload.ShouldBe(SomePayload);
        initPayload.ShouldBe(SomePayload);
    }

    [Fact]
    public void None_is_written_as_null_and_the_property_stays()
    {
        var positionalPayload = MessageSerializer.Serialize(NonePositional);
        var initPayload = MessageSerializer.Serialize(NoneInit);

        positionalPayload.ShouldBe(NullPayload);
        initPayload.ShouldBe(NullPayload);
    }

    [Fact]
    public void Value_is_read_as_some()
    {
        var positionalResult = Deserialize<PositionalMessage>(SomePayload);
        var initResult = Deserialize<InitMessage>(SomePayload);

        positionalResult.Value.ShouldBe(SomePositional);
        initResult.Value.ShouldBe(SomeInit);
    }

    [Fact]
    public void Null_is_read_as_none()
    {
        var positionalResult = Deserialize<PositionalMessage>(NullPayload);
        var initResult = Deserialize<InitMessage>(NullPayload);

        positionalResult.Value.ShouldBe(NonePositional);
        initResult.Value.ShouldBe(NoneInit);
    }

    [Fact]
    public void Missing_property_of_an_older_payload_is_read_as_none()
    {
        var positionalResult = Deserialize<PositionalMessage>(OlderPayload);
        var initResult = Deserialize<InitMessage>(OlderPayload);

        positionalResult.Value.ShouldBe(NonePositional);
        initResult.Value.ShouldBe(NoneInit);
    }

    [Theory]
    [InlineData("""{"label":"label","text":5}""")]
    [InlineData("""{"label":"label","number":"five"}""")]
    [InlineData("""{"label":"label","inner":{"name":"name","size":"big"}}""")]
    public void Value_of_the_wrong_type_is_an_invalid_payload(String payload)
    {
        var positionalResult = Deserialize<PositionalMessage>(payload);
        var initResult = Deserialize<InitMessage>(payload);

        positionalResult.Error.ShouldBe(MessagingErrors.InvalidPayload);
        initResult.Error.ShouldBe(MessagingErrors.InvalidPayload);
    }

    private static Result<TMessage> Deserialize<TMessage>(String payload)
        where TMessage : notnull =>
        MessageSerializer.Deserialize<TMessage>(payload, typeof(TMessage));

    private sealed record PositionalMessage(
        String Label,
        Optional<String> Text,
        Optional<Int32> Number,
        Optional<SampleRecord> Inner);

    private sealed record InitMessage
    {
        public required String Label { get; init; }

        public Optional<String> Text { get; init; }

        public Optional<Int32> Number { get; init; }

        public Optional<SampleRecord> Inner { get; init; }
    }

    private sealed record SampleRecord(String Name, Int32 Size);
}
