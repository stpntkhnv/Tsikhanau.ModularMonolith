using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class CommandLineWords
{
    public static Boolean IsName(String argument) => !argument.StartsWith('-');

    public static Error BuildUsageError(String message) => Error.Failure("scaffold.usage", message);
}
