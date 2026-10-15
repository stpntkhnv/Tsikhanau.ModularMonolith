using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.Hosting;

public static partial class HostLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Host terminated unexpectedly")]
    public static partial void Terminated(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"{DatabaseSettings.Section}:{nameof(DatabaseSettings.PrepareMode)} is Only: the database is "
            + "prepared, and the host stops without serving requests or running background work")]
    internal static partial void PreparedOnly(ILogger logger);

    public static void TerminatedBeforeTheHostWasBuilt(Exception exception)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        Terminated(loggerFactory.CreateLogger(AppDomain.CurrentDomain.FriendlyName), exception);
    }
}
