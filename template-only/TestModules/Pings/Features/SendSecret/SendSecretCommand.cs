using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;

internal sealed record SendSecretCommand(Guid PingId, String Secret) : IPingsCommand<Unit>;
