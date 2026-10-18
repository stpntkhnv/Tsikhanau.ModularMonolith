using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Dispatching;

internal sealed record CreatePingThenThrowCommand(Guid PingId) : IPingsCommand<Unit>, ICommitOnFailure;
