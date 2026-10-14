using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.TwiceHandled;

public sealed class TwiceHandledCommandSecondHandler : ICommandHandler<TwiceHandledCommand, Unit>
{
    public Task<Result<Unit>> Handle(TwiceHandledCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
