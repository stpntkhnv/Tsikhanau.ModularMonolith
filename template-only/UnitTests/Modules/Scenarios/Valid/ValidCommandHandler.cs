using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Valid;

public sealed class ValidCommandHandler : ICommandHandler<ValidCommand, Unit>
{
    public Task<Result<Unit>> Handle(ValidCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
