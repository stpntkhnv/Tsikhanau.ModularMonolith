namespace ModularMonolith.TestModules.Pings.Features;

internal sealed class PingTestHooks
{
    public Func<Task> AfterLoad { get; set; } = static () => Task.CompletedTask;
}
