using Acme.Modules.Bad.Features.RenameCrate;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Infrastructure.Relays;

internal sealed class CrateRelay(ICommandHandler<RenameCrateCommand, Unit> handler)
{
    public Task<Result<Unit>> RelayAsync(RenameCrateCommand command, CancellationToken cancellationToken) =>
        handler.Handle(command, cancellationToken);
}
