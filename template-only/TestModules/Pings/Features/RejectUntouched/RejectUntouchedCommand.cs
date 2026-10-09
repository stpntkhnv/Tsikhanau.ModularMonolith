using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RejectUntouched;

internal sealed record RejectUntouchedCommand(Guid PingId) : IPingsCommand<Unit>, ICommitOnFailure;
