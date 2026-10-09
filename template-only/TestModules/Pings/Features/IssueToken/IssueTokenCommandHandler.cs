using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.IssueToken;

internal sealed class IssueTokenCommandHandler(IOutbox<IPingsModule> outbox, TimeProvider timeProvider)
    : ICommandHandler<IssueTokenCommand, Unit>
{
    public Task<Result<Unit>> Handle(IssueTokenCommand command, CancellationToken cancellationToken)
    {
        outbox.Add(new PingTokenIssuedIntegrationEvent(timeProvider.GetUtcNow(), command.PingId, command.Token));
        return Task.FromResult(Result.Success());
    }
}
