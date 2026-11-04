using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class ModuleCommandLine
{
    public const String Synopsis = $"bash scripts/scaffold.sh module <Name> [{SettingsOption}]";

    private const String SettingsOption = "--settings";

    public static Result<IScaffold> Parse(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [var name] when CommandLineWords.IsName(name) => CreateScaffold(name, hasSettings: false),
            [var name, SettingsOption] when CommandLineWords.IsName(name) => CreateScaffold(name, hasSettings: true),
            _ => CommandLineWords.BuildUsageError(
                $"module takes one name and optionally {SettingsOption}; usage: {Synopsis}"),
        };

    private static Result<IScaffold> CreateScaffold(String name, Boolean hasSettings) =>
        Result.Success<IScaffold>(new ModuleScaffold(name, hasSettings));
}
