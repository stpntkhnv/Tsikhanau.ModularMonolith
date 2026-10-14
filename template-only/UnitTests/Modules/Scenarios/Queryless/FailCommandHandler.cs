using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Queryless;

public sealed class FailCommandHandler : ICommandHandler<FailCommand, Unit>
{
    public Task<Result<Unit>> Handle(FailCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(command.Error));
}
