using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.UnnamedInternalCommand;

public sealed class UnnamedInternalCommandHandler : ICommandHandler<UnnamedInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(UnnamedInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
