using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.TouchPingWithRetry;

internal sealed record TouchPingWithRetryCommand(Guid PingId) : IPingsCommand<Unit>, IRetryOnConflict;
