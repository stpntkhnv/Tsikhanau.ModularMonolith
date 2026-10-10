using System.Text;
using System.Text.Json;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static class JsonOutlineReader
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static Optional<JsonObjectOutline> Read(String text)
    {
        var json = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(json, ReaderOptions);

        return reader.Read() && reader.TokenType == JsonTokenType.StartObject
            ? ReadObject(ref reader, json)
            : Optional<JsonObjectOutline>.None();
    }

    private static JsonObjectOutline ReadObject(ref Utf8JsonReader reader, Byte[] json)
    {
        var start = ToTextOffset(json, reader.TokenStartIndex);
        var properties = new List<JsonPropertyOutline>();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            properties.Add(ReadProperty(ref reader, json));
        }

        return new JsonObjectOutline(start, ToTextOffset(json, reader.TokenStartIndex), properties);
    }

    private static JsonPropertyOutline ReadProperty(ref Utf8JsonReader reader, Byte[] json)
    {
        var name = reader.GetString() ?? String.Empty;
        var nameStart = ToTextOffset(json, reader.TokenStartIndex);
        reader.Read();
        var objectValue = ReadObjectValue(ref reader, json);

        return new JsonPropertyOutline(name, nameStart, ToTextOffset(json, reader.BytesConsumed), objectValue);
    }

    private static Optional<JsonObjectOutline> ReadObjectValue(ref Utf8JsonReader reader, Byte[] json)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadObject(ref reader, json);
        }

        reader.Skip();
        return Optional<JsonObjectOutline>.None();
    }

    private static Int32 ToTextOffset(Byte[] json, Int64 byteOffset) =>
        Encoding.UTF8.GetCharCount(json, 0, (Int32)byteOffset);
}
