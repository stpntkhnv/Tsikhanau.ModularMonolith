using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;

[InternalCommandName("remind")]
public sealed record RemindInternalCommand(Guid Id) : InternalCommand(Id);

[InternalCommandName("remind")]
public sealed record RemindAgainInternalCommand(Guid Id) : InternalCommand(Id);

public sealed class RemindInternalCommandHandler : ICommandHandler<RemindInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemindInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class RemindAgainInternalCommandHandler : ICommandHandler<RemindAgainInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemindAgainInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
