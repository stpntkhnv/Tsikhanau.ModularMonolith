using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Contracts.Secrets;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.MailToken;

[InternalCommandName("mail-token")]
[ContainsSecrets]
internal sealed record MailTokenInternalCommand(Guid Id, Guid PingId, String Token)
    : InternalCommand(Id), IPingsCommand<Unit>;
