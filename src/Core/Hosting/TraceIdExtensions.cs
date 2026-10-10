using System.Text.Json;

namespace Tsikhanau.Monolith.Hosting;

internal static class TraceIdExtensions
{
    private const String TraceIdName = "traceId";

    extension(JsonSerializerOptions serializerOptions)
    {
        public String TraceIdKey => serializerOptions.PropertyNamingPolicy?.ConvertName(TraceIdName) ?? TraceIdName;
    }
}
