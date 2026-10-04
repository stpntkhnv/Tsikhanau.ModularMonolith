using ModularMonolith.BuildingBlocks.Application.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.MixedTransaction;

public sealed class OutsideHandler : IIntegrationEventHandler<MixedHappenedIntegrationEvent>, IRunOutsideTransaction
{
    public Task<Result<Unit>> Handle(
        MixedHappenedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
