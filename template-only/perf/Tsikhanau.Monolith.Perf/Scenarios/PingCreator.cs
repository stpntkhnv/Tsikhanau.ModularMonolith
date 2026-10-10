using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class PingCreator
{
    private Int64 _createdCount;

    public async Task<Guid> CreateAsync(IPingsModule pingsModule, CancellationToken cancellationToken)
    {
        var command = new CreatePingCommand(Guid.CreateVersion7(), BuildNextLabel(), Optional<String>.None());
        var result = await pingsModule.ExecuteCommandAsync(command, cancellationToken);
        return result.GetValueOrThrow();
    }

    private String BuildNextLabel() => $"perf {Interlocked.Increment(ref _createdCount)}";
}
