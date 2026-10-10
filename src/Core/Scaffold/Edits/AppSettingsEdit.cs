using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal sealed class AppSettingsEdit(JsonText json, String path, String moduleName)
{
    private const String App = "App";

    private const String Modules = "Modules";

    private const String ModulesPath = $"{App}:{Modules}";

    private const String LayoutRule = "with one entry per line and its closing brace on a line of its own";

    public static Result<FileEdit> AddModule(TextFile appSettings, ScaffoldedModule module) =>
        appSettings.PlanEdit(text =>
            new AppSettingsEdit(new JsonText(text, appSettings.NewLine), appSettings.Path, module.Name)
                .AddModuleSection());

    private String Entry => $"\"{moduleName}\": {{}}";

    private Result<String> AddModuleSection() =>
        JsonOutlineReader
            .Read(json.Text)
            .Bind(root => root.FindObject(App))
            .ToResult(BuildAnchorError($"an object {App}"))
            .Bind(AddToApp);

    private Result<String> AddToApp(JsonObjectOutline app)
    {
        var indentationStep = json.ReadIndentation(app.Start);

        return app
            .FindProperty(Modules)
            .Match(
                modulesProperty => modulesProperty.ObjectValue
                    .ToResult(BuildAnchorError($"{ModulesPath} as an object"))
                    .Bind(modules => AddToModules(modules, indentationStep)),
                () => AppendModulesSection(app, indentationStep));
    }

    private Result<String> AppendModulesSection(JsonObjectOutline app, String indentationStep) =>
        Append(app, App, BuildModulesSection(GetPropertyIndentation(app, indentationStep), indentationStep));

    private Result<String> AddToModules(JsonObjectOutline modules, String indentationStep) =>
        Result
            .Success(modules)
            .Ensure(HasNoModuleSection, BuildAnchorError($"no {ModulesPath}:{moduleName} yet"))
            .Bind(_ => FindLaterModule(modules)
                .Match(InsertBefore, () => AppendModule(modules, indentationStep)));

    private Result<String> AppendModule(JsonObjectOutline modules, String indentationStep) =>
        Append(modules, ModulesPath, [$"{GetPropertyIndentation(modules, indentationStep)}{Entry}"]);

    private Boolean HasNoModuleSection(JsonObjectOutline modules) => modules.FindProperty(moduleName).IsNone;

    private Optional<JsonPropertyOutline> FindLaterModule(JsonObjectOutline modules) =>
        modules.Properties.FirstOrDefault(moduleSection => String.CompareOrdinal(moduleSection.Name, moduleName) > 0);

    private Result<String> InsertBefore(JsonPropertyOutline laterModule) =>
        json.IsFirstOnLine(laterModule.NameStart)
            ? json.Text.Insert(
                json.GetLineStart(laterModule.NameStart),
                $"{json.ReadIndentation(laterModule.NameStart)}{Entry},{json.NewLine}")
            : BuildLayoutError(ModulesPath);

    private Result<String> Append(JsonObjectOutline target, String targetPath, IReadOnlyList<String> lines) =>
        target switch
        {
            _ when json.IsFirstOnLine(target.End) => AppendBeforeClosingLine(target, lines),
            { IsEmpty: true } when json.IsBlank(target.Start + 1, target.End) => ExpandEmptyObject(target, lines),
            _ => BuildLayoutError(targetPath),
        };

    private String AppendBeforeClosingLine(JsonObjectOutline target, IReadOnlyList<String> lines)
    {
        var withLines = json.Text.Insert(json.GetLineStart(target.End), JoinLines(lines));

        return target.Properties is [.., var last] && !json.HasCommaBetween(last.ValueEnd, target.End)
            ? withLines.Insert(last.ValueEnd, ",")
            : withLines;
    }

    private String ExpandEmptyObject(JsonObjectOutline target, IReadOnlyList<String> lines) =>
        String.Concat(
            json.Text[..(target.Start + 1)],
            json.NewLine,
            JoinLines(lines),
            json.ReadIndentation(target.Start),
            json.Text[target.End..]);

    private String GetPropertyIndentation(JsonObjectOutline target, String indentationStep) =>
        target.Properties is [.., var last]
            ? json.ReadIndentation(last.NameStart)
            : json.ReadIndentation(target.Start) + indentationStep;

    private IReadOnlyList<String> BuildModulesSection(String indentation, String indentationStep) =>
        [$"{indentation}\"{Modules}\": {{", $"{indentation}{indentationStep}{Entry}", $"{indentation}}}"];

    private String JoinLines(IReadOnlyList<String> lines) =>
        String.Concat(lines.Select(line => line + json.NewLine));

    private Error BuildLayoutError(String targetPath) => BuildAnchorError($"{targetPath} {LayoutRule}");

    private Error BuildAnchorError(String expected) => Error.Failure("scaffold.anchor", $"{path} must hold {expected}");
}
