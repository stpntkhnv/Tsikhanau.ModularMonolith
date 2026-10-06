using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.Valid;

public sealed record ValidCommand : ICommand<Unit>;

public sealed class ValidCommandHandler : ICommandHandler<ValidCommand, Unit>
{
    public Task<Result<Unit>> Handle(ValidCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

[InternalCommandName("valid")]
public sealed record ValidInternalCommand(Guid Id) : InternalCommand(Id);

public sealed class ValidInternalCommandHandler : ICommandHandler<ValidInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(ValidInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
