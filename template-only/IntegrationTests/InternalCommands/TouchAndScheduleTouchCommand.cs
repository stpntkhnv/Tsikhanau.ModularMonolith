using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.InternalCommands;

internal sealed record TouchAndScheduleTouchCommand(Guid PingId) : IPingsCommand<Unit>;
