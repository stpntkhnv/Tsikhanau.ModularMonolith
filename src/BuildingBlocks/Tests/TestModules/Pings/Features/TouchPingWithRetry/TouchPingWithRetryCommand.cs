using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.TouchPingWithRetry;

internal sealed record TouchPingWithRetryCommand(Guid PingId) : IPingsCommand<Unit>, IRetryOnConflict;
