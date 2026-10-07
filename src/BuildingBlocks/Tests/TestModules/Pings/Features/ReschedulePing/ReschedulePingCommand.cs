using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.ReschedulePing;

internal sealed record ReschedulePingCommand(Guid PingId, TimeSpan Interval, Int32 Repeats) : IPingsCommand<Unit>;
