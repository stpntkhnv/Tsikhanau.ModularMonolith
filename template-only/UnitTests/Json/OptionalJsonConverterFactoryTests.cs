using System.Text.Json;
using Tsikhanau.Monolith.Contracts.Json;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Json;

public sealed class OptionalJsonConverterFactoryTests
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new OptionalJsonConverterFactory() },
    };

    [Theory]
    [InlineData("""{"text":5}""", "$.text")]
    [InlineData("""{"number":"five"}""", "$.number")]
    [InlineData("""{"inner":{"name":"name","size":"big"}}""", "$.inner")]
    public void Value_of_the_wrong_type_is_reported_at_the_optional_property(String json, String path)
    {
        var positionalPath = GetErrorPath<PositionalMessage>(json);
        var initPath = GetErrorPath<InitMessage>(json);

        positionalPath.ShouldBe(path);
        initPath.ShouldBe(path);
    }

    private static String? GetErrorPath<TMessage>(String json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TMessage>(json, JsonSerializerOptions)).Path;

    private sealed record PositionalMessage(
        Optional<String> Text,
        Optional<Int32> Number,
        Optional<SampleRecord> Inner);

    private sealed record InitMessage
    {
        public Optional<String> Text { get; init; }

        public Optional<Int32> Number { get; init; }

        public Optional<SampleRecord> Inner { get; init; }
    }

    private sealed record SampleRecord(String Name, Int32 Size);
}
