using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.MailToken;

internal sealed class MailTokenInternalCommandHandler(IPingRepository pingRepository, PingTestHooks pingTestHooks)
    : ICommandHandler<MailTokenInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(MailTokenInternalCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .TapAsync(_ => pingTestHooks.MailedTokens.Enqueue(command.Token))
            .MapAsync(_ => Unit.Value);
}
