using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class ScaffoldCommandLine
{
    private const String SettingsOption = "--settings";

    private const String ModuleSynopsis = $"bash scripts/scaffold.sh module <Name> [{SettingsOption}]";

    private const String Usage = $"usage: {ModuleSynopsis}, or {FeatureCommandLine.Synopsis}";

    public static Result<IScaffoldCommand> Parse(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            ["module", var name] when IsName(name) => CreateModuleCommand(name, hasSettings: false),
            ["module", var name, SettingsOption] when IsName(name) => CreateModuleCommand(name, hasSettings: true),
            ["module", ..] => Error.Failure(
                "scaffold.usage",
                $"module takes one name and optionally {SettingsOption}; usage: {ModuleSynopsis}"),
            ["feature", ..] => FeatureCommandLine.Parse([.. arguments.Skip(1)]),
            [] => Error.Failure("scaffold.usage", $"no command given; {Usage}"),
            [var command, ..] => Error.Failure("scaffold.usage", $"unknown command '{command}'; {Usage}"),
        };

    public static Boolean IsName(String argument) => !argument.StartsWith('-');

    private static Result<IScaffoldCommand> CreateModuleCommand(String name, Boolean hasSettings) =>
        Result.Success<IScaffoldCommand>(new ModuleCommand(name, hasSettings));
}
