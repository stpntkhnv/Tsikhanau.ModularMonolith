using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static partial class DatabasePreparationLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for the database preparation lock")]
    public static partial void WaitingForLock(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Prepared the database {Database} for the modules {Modules}")]
    public static partial void Prepared(ILogger logger, String database, String modules);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Checked through the module logins that the database {Database} is ready for the modules {Modules}")]
    public static partial void Verified(ILogger logger, String database, String modules);
}
