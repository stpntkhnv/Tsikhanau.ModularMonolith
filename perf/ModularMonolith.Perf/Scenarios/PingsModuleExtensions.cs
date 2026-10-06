using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;

namespace ModularMonolith.Perf.Scenarios;

internal static class PingsModuleExtensions
{
    extension(IPingsModule pingsModule)
    {
        public async Task<PingDto> GetPingAsync(Guid pingId, CancellationToken cancellationToken)
        {
            var result = await pingsModule.ExecuteQueryAsync(new GetPingQuery(pingId), cancellationToken);
            return result.GetValueOrThrow();
        }
    }
}
