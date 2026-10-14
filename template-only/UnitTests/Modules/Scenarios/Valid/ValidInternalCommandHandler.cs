using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Valid;

public sealed class ValidInternalCommandHandler : ICommandHandler<ValidInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(ValidInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
