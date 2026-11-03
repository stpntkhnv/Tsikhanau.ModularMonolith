using System.Text.Json;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.Hosting.Problems;

internal static class TraceIdExtensions
{
    extension(JsonSerializerOptions serializerOptions)
    {
        public String TraceIdKey =>
            serializerOptions.PropertyNamingPolicy?.ConvertName(ProblemMembers.TraceId) ?? ProblemMembers.TraceId;
    }
}
