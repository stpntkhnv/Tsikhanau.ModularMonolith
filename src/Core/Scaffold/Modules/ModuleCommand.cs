using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class ModuleCommand(String name, Boolean hasSettings) : IScaffoldCommand
{
    private const String MigrationName = "Initial";

    public Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem) =>
        ModuleName
            .Parse(name, app.LoginPrefix)
            .Map(moduleName => new ScaffoldedModule(app, moduleName))
            .Bind(module => EnsureNoSuchModule(module, fileSystem))
            .Bind(PlanModule);

    private static Result<ScaffoldedModule> EnsureNoSuchModule(ScaffoldedModule module, IFileSystem fileSystem)
    {
        Optional<String> existingFolder = fileSystem
            .GetDirectories(ScaffoldedModule.ModulesFolder, "*")
            .FirstOrDefault(folder => IsFolderOf(folder, module));

        return existingFolder.Match(
            folder => Error.Failure("scaffold.exists", $"{folder} already exists"),
            () => Result.Success(module));
    }

    private static Boolean IsFolderOf(String folder, ScaffoldedModule module) =>
        Path
            .GetFileName(folder)
            .Equals(module.Name, StringComparison.OrdinalIgnoreCase);

    private Result<ScaffoldPlan> PlanModule(ScaffoldedModule module) =>
        Result
            .Collect(CreateFiles(module))
            .And(PlanEdits(module))
            .Map((newFiles, fileEdits) => new ScaffoldPlan(newFiles, fileEdits, [CreateMigrationStep(module)]));

    private Result<IReadOnlyList<NewFile>> CreateFiles(ScaffoldedModule module) =>
        CodeLayout.EnsureLinesFit(
            new ModuleTemplate(module, hasSettings).CreateFiles(),
            $"module name '{module.Name}'",
            "name");

    private Result<IReadOnlyList<FileEdit>> PlanEdits(ScaffoldedModule module) =>
        Result.CombineAll(
        [
            SolutionEdit.AddModule(module.App.Solution, module),
            HostProjectEdit.AddModule(module.App.HostProject, module),
            ProgramEdit.AddModule(module.App.Program, module),
            .. PlanSettingsEdits(module),
        ]);

    private IEnumerable<Result<FileEdit>> PlanSettingsEdits(ScaffoldedModule module) =>
        hasSettings ? [AppSettingsEdit.AddModule(module.App.AppSettings, module)] : [];

    private static ExternalStep CreateMigrationStep(ScaffoldedModule module) =>
        new("bash", ["scripts/add-migration.sh", module.Name, MigrationName]);
}
