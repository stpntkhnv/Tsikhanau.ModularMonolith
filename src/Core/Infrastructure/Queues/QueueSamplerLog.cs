using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal static partial class QueueSamplerLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Module} queue sample did not finish within {Timeout}; its counts keep the last values")]
    public static partial void TimedOut(ILogger logger, String module, TimeSpan timeout);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Module} queue sample failed; its counts keep the last values")]
    public static partial void Failed(ILogger logger, String module, Exception exception);
}
