using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static partial class DatabasePreparationLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for the database preparation lock")]
    public static partial void WaitingForLock(ILogger logger);
}
