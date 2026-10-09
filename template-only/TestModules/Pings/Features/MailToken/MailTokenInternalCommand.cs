using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Contracts.Secrets;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.MailToken;

[InternalCommandName("mail-token")]
[ContainsSecrets]
internal sealed record MailTokenInternalCommand(Guid Id, Guid PingId, String Token)
    : InternalCommand(Id), IPingsCommand<Unit>;
