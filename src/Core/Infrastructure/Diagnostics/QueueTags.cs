namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

internal static class QueueTags
{
    private const String Module = "module";

    private const String Queue = "queue";

    private const String Outcome = "outcome";

    public static KeyValuePair<String, Object?>[] Create(String module, String queue) =>
        [new(Module, module), new(Queue, queue)];

    public static KeyValuePair<String, Object?>[] Create(String module, String queue, String outcome) =>
        [new(Module, module), new(Queue, queue), new(Outcome, outcome)];
}
