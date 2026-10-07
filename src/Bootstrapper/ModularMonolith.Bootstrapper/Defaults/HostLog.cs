namespace ModularMonolith.Bootstrapper.Defaults;

internal static partial class HostLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Host terminated unexpectedly")]
    public static partial void Terminated(ILogger logger, Exception exception);

    public static void TerminatedBeforeTheHostWasBuilt(Exception exception)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        Terminated(loggerFactory.CreateLogger(AppDomain.CurrentDomain.FriendlyName), exception);
    }
}
