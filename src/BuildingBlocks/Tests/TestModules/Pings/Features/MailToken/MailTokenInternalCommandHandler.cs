using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.MailToken;

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
