using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed record LogEntry(
    String Category,
    LogLevel Level,
    String Message,
    IReadOnlyList<String> StateValues,
    IReadOnlyList<String> Scopes,
    String Exception)
{
    public Boolean Contains(String text) =>
        Texts.Any(entryText => entryText.Contains(text, StringComparison.Ordinal));

    private IEnumerable<String> Texts => [Message, Exception, .. StateValues, .. Scopes];
}
