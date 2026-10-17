using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal static class QueuesCommandLine
{
    public const String Usage = """
        usage: queues list
               queues retry <module> <queue> <id>
               queues discard <module> <queue> <id>
        """;

    private const String UsageCode = "Queues.Usage";

    public static Result<IQueuesCommand> Parse(IReadOnlyList<String> arguments, ModuleRegistry moduleRegistry) =>
        arguments switch
        {
            ["list"] => Result.Success<IQueuesCommand>(new ListCommand()),
            ["retry", var moduleName, var queue, var messageId] =>
                ParseAddress(moduleRegistry, moduleName, queue, messageId)
                    .Map(CreateRetryCommand),
            ["discard", var moduleName, var queue, var messageId] =>
                ParseAddress(moduleRegistry, moduleName, queue, messageId)
                    .Map(CreateDiscardCommand),
            ["list", ..] => BuildUsageError("list takes no arguments"),
            [var command and ("retry" or "discard"), ..] =>
                BuildUsageError($"{command} takes a module, a queue and a message id"),
            [] => BuildUsageError("no command given"),
            [var command, ..] => BuildUsageError($"unknown command '{command}'"),
        };

    private static Result<MessageAddress> ParseAddress(
        ModuleRegistry moduleRegistry,
        String moduleName,
        String queue,
        String messageId) =>
        Result
            .Collect(FindModule(moduleRegistry, moduleName))
            .And(ParseQueue(queue))
            .And(ParseMessageId(messageId))
            .Map((module, knownQueue, parsedMessageId) => new MessageAddress(module, knownQueue, parsedMessageId));

    private static Result<ModuleDescriptor> FindModule(ModuleRegistry moduleRegistry, String moduleName) =>
        Optional<ModuleDescriptor>
            .FromNullable(moduleRegistry.Modules.FirstOrDefault(module => IsNamed(module, moduleName)))
            .ToResult(() => BuildUsageError($"unknown module '{moduleName}'; {DescribeModules(moduleRegistry)}"));

    private static Boolean IsNamed(ModuleDescriptor module, String moduleName) =>
        String.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase);

    private static String DescribeModules(ModuleRegistry moduleRegistry) =>
        moduleRegistry.Modules.Count == 0
            ? "no modules are registered"
            : $"the modules are {JoinModuleNames(moduleRegistry)}";

    private static String JoinModuleNames(ModuleRegistry moduleRegistry)
    {
        var moduleNames = moduleRegistry.Modules
            .Select(module => module.Name)
            .Order(StringComparer.Ordinal);

        return String.Join(", ", moduleNames);
    }

    private static Result<String> ParseQueue(String queue) =>
        QueueTables.Names.Contains(queue)
            ? queue
            : BuildUsageError($"unknown queue '{queue}'; the queues are {String.Join(", ", QueueTables.Names)}");

    private static Result<Guid> ParseMessageId(String messageId) =>
        Guid.TryParse(messageId, out var parsedMessageId)
            ? parsedMessageId
            : BuildUsageError($"'{messageId}' is not a message id: it must be a Guid");

    private static IQueuesCommand CreateRetryCommand(MessageAddress message) => new RetryCommand(message);

    private static IQueuesCommand CreateDiscardCommand(MessageAddress message) => new DiscardCommand(message);

    private static Error BuildUsageError(String reason) => Error.Failure(UsageCode, reason);
}
