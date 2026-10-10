using System.Text.Json;
using Tsikhanau.Monolith.Contracts.Json;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static String Serialize(Object message) => JsonSerializer.Serialize(message, message.GetType(), Options);

    public static Result<TMessage> Deserialize<TMessage>(String payload, Type type)
        where TMessage : notnull
    {
        try
        {
            return JsonSerializer.Deserialize(payload, type, Options) is TMessage message
                ? message
                : MessagingErrors.InvalidPayload;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return MessagingErrors.InvalidPayload;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new OptionalJsonConverterFactory());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
