using ModularMonolith.BuildingBlocks.Application.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.MixedTransaction;

public sealed class InsideHandler : IIntegrationEventHandler<MixedHappenedIntegrationEvent>
{
    public Task<Result<Unit>> Handle(
        MixedHappenedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
