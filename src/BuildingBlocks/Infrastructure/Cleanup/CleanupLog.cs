using Microsoft.Extensions.Logging;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;

internal static partial class CleanupLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{Module} cleanup failed")]
    public static partial void Failed(ILogger logger, String module, Exception exception);
}
