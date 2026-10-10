using System.Text.Json;
using System.Text.Json.Serialization;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Contracts.Json;

public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override Boolean CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        CreateInstance(typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()));

    private static JsonConverter CreateInstance(Type converterType) =>
        (JsonConverter)Activator.CreateInstance(converterType)!;
}
