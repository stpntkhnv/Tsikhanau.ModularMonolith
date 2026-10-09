using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed record LogEntry(String Category, LogLevel Level, String Message);
