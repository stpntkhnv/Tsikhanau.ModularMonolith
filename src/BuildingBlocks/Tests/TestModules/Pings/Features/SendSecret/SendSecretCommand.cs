using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.SendSecret;

internal sealed record SendSecretCommand(Guid PingId, String Secret) : IPingsCommand<Unit>;
