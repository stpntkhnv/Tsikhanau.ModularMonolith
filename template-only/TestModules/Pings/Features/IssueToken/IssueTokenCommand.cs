using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.IssueToken;

internal sealed record IssueTokenCommand(Guid PingId, String Token) : IPingsCommand<Unit>;
