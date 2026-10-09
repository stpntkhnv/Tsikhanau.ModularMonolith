using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ReschedulePing;

internal sealed record ReschedulePingCommand(Guid PingId, TimeSpan Interval, Int32 Repeats) : IPingsCommand<Unit>;
