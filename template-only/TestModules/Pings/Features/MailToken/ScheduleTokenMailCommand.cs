using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.MailToken;

internal sealed record ScheduleTokenMailCommand(Guid PingId, String Token) : IPingsCommand<Unit>;
