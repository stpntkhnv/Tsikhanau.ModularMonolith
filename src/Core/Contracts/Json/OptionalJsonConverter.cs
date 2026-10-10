using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Contracts.Json;

internal sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    where T : notnull
{
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? Optional<T>.None()
            : ReadValue(ref reader, options);

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            JsonSerializer.Serialize(writer, value.Value, GetValueTypeInfo(options));
            return;
        }

        writer.WriteNullValue();
    }

    private static Optional<T> ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        try
        {
            return Optional<T>.FromNullable(JsonSerializer.Deserialize(ref reader, GetValueTypeInfo(options)));
        }
        catch (JsonException exception)
        {
            throw new JsonException(null, exception);
        }
    }

    private static JsonTypeInfo<T> GetValueTypeInfo(JsonSerializerOptions options) =>
        (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
