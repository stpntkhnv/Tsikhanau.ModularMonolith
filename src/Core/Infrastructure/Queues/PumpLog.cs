using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal static partial class PumpLog
{
    private static readonly Func<ILogger, String, String, Guid, String, IDisposable?> MessageScope =
        LoggerMessage.DefineScope<String, String, Guid, String>("{Module} {Table} message {MessageId} of type {Type}");

    public static IDisposable? BeginMessageScope(
        ILogger logger,
        String module,
        String table,
        Guid messageId,
        String type) =>
        MessageScope(logger, module, table, messageId, type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message will be retried: {Reason}")]
    public static partial void Retrying(ILogger logger, String reason, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Message failed: {Reason}")]
    public static partial void Failed(ILogger logger, String reason, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Message failed after {Attempts} attempts: {Reason}")]
    public static partial void GaveUp(ILogger logger, Int32 attempts, String reason, Exception? exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Module} {Table} claims one message at a time for a poll interval ({Reason}); "
            + "gave back {GivenBackCount} message(s)")]
    public static partial void ClaimsOneAtATime(
        ILogger logger,
        String module,
        String table,
        String reason,
        Int32 givenBackCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Module} {Table} could not give back {Count} message(s); they return after their lease")]
    public static partial void GiveBackFailed(
        ILogger logger,
        String module,
        String table,
        Int32 count,
        Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Module} queue loop failed")]
    public static partial void LoopFailed(ILogger logger, String module, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Module} handler abandoned at shutdown; the message returns after its lease")]
    public static partial void AbandonedAtShutdown(ILogger logger, String module);
}
