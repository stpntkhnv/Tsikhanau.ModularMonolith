using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.IssueToken;

internal sealed record IssueTokenCommand(Guid PingId, String Token) : IPingsCommand<Unit>;
