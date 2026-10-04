using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.TwiceHandled;

public sealed record TwiceHandledCommand : ICommand<Unit>;

public sealed class TwiceHandledCommandFirstHandler : ICommandHandler<TwiceHandledCommand, Unit>
{
    public Task<Result<Unit>> Handle(TwiceHandledCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class TwiceHandledCommandSecondHandler : ICommandHandler<TwiceHandledCommand, Unit>
{
    public Task<Result<Unit>> Handle(TwiceHandledCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
