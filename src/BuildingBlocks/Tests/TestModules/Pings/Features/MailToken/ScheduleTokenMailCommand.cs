using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.MailToken;

internal sealed record ScheduleTokenMailCommand(Guid PingId, String Token) : IPingsCommand<Unit>;
