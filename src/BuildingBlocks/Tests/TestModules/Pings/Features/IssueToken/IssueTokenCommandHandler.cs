using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.IssueToken;

internal sealed class IssueTokenCommandHandler(IOutbox<IPingsModule> outbox, TimeProvider timeProvider)
    : ICommandHandler<IssueTokenCommand, Unit>
{
    public Task<Result<Unit>> Handle(IssueTokenCommand command, CancellationToken cancellationToken)
    {
        outbox.Add(new PingTokenIssuedIntegrationEvent(timeProvider.GetUtcNow(), command.PingId, command.Token));
        return Task.FromResult(Result.Success());
    }
}
