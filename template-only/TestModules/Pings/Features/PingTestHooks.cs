using System.Collections.Concurrent;

namespace Tsikhanau.Monolith.TestModules.Pings.Features;

internal sealed class PingTestHooks
{
    public Func<Task> AfterLoad { get; set; } = static () => Task.CompletedTask;

    public ConcurrentQueue<String> MailedTokens { get; } = new();
}
