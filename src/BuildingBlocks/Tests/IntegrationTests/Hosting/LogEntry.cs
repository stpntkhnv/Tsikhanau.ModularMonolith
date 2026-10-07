using Microsoft.Extensions.Logging;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;

internal sealed record LogEntry(String Category, LogLevel Level, String Message);
