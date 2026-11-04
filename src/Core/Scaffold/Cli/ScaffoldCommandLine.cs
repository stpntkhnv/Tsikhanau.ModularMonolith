using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class ScaffoldCommandLine
{
    private const String Usage = $"usage: {ModuleCommandLine.Synopsis}, or {FeatureCommandLine.Synopsis}";

    public static Result<IScaffold> Parse(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            ["module", ..] => ModuleCommandLine.Parse([.. arguments.Skip(1)]),
            ["feature", ..] => FeatureCommandLine.Parse([.. arguments.Skip(1)]),
            [] => CommandLineWords.BuildUsageError($"no command given; {Usage}"),
            [var command, ..] => CommandLineWords.BuildUsageError($"unknown command '{command}'; {Usage}"),
        };
}
