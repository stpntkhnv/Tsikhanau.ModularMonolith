using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class ScaffoldCommandLine
{
    private const String Usage =
        "usage: bash scripts/scaffold.sh <command> [<arguments>]; this version has no commands yet";

    public static Result<IScaffoldCommand> Parse(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [] => Error.Failure("scaffold.usage", $"no command given; {Usage}"),
            [var command, ..] => Error.Failure("scaffold.usage", $"unknown command '{command}'; {Usage}"),
        };
}
