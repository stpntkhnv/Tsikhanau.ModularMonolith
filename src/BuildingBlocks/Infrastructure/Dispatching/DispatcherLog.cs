using Microsoft.Extensions.Logging;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;

internal static partial class DispatcherLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Module} {Request} succeeded in {ElapsedMs} ms")]
    public static partial void Succeeded(ILogger logger, String module, String request, Double elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Module} {Request} failed with {ErrorCode} in {ElapsedMs} ms")]
    public static partial void Failed(
        ILogger logger,
        String module,
        String request,
        String errorCode,
        Double elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Module} {Request} failed unexpectedly with {ErrorCode} in {ElapsedMs} ms")]
    public static partial void FailedUnexpectedly(
        ILogger logger,
        String module,
        String request,
        String errorCode,
        Double elapsedMs);
}
