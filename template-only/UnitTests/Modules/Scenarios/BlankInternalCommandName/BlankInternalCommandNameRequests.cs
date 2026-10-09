using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.BlankInternalCommandName;

[InternalCommandName(" ")]
public sealed record BlankNamedInternalCommand(Guid Id) : InternalCommand(Id);

public sealed class BlankNamedInternalCommandHandler : ICommandHandler<BlankNamedInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(BlankNamedInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
