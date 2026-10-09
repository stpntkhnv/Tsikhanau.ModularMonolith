using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.MixedTransaction;

public sealed class InsideHandler : IIntegrationEventHandler<MixedHappenedIntegrationEvent>
{
    public Task<Result<Unit>> Handle(
        MixedHappenedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
