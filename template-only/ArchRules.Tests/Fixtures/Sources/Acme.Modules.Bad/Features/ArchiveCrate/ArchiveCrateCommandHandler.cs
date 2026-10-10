using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.ArchiveCrate;

internal class ArchiveCrateCommandHandler : ICommandHandler<ArchiveCrateCommand, Unit>
{
    public Task<Result<Unit>> Handle(ArchiveCrateCommand command, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Unit>>(Unit.Value);
}
