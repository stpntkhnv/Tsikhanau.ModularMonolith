using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.InspectPing;

internal sealed class InspectPingCommandHandler(IPingRepository pingRepository)
    : ICommandHandler<InspectPingCommand, Unit>
{
    public Task<Result<Unit>> Handle(InspectPingCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .MapAsync(_ => Unit.Value);
}
