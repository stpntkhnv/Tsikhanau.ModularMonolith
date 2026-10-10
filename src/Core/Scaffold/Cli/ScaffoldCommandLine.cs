using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class ScaffoldCommandLine
{
    private const String Usage = "usage: bash scripts/scaffold.sh module <Name>";

    public static Result<IScaffoldCommand> Parse(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            ["module", var name] => Result.Success<IScaffoldCommand>(new ModuleCommand(name)),
            ["module", ..] => Error.Failure("scaffold.usage", $"module takes exactly one name; {Usage}"),
            [] => Error.Failure("scaffold.usage", $"no command given; {Usage}"),
            [var command, ..] => Error.Failure("scaffold.usage", $"unknown command '{command}'; {Usage}"),
        };
}
