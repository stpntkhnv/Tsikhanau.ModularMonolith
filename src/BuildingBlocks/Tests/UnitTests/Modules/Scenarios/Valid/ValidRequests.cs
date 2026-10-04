using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.Valid;

public sealed record ValidCommand : ICommand<Unit>;

public sealed class ValidCommandHandler : ICommandHandler<ValidCommand, Unit>
{
    public Task<Result<Unit>> Handle(ValidCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
