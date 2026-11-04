using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class ModuleScaffold(String name, Boolean hasSettings) : IScaffold
{
    private const String MigrationName = "Initial";

    public Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem) =>
        ScaffoldedModule
            .Parse(app, name)
            .Bind(module => EnsureNoSuchModule(module, fileSystem))
            .Bind(PlanModule);

    private static Result<ScaffoldedModule> EnsureNoSuchModule(ScaffoldedModule module, IFileSystem fileSystem)
    {
        Optional<String> existingName = ModuleFolders
            .ReadNames(fileSystem)
            .FirstOrDefault(folderName => folderName.Equals(module.Name, StringComparison.OrdinalIgnoreCase));

        return existingName.Match(
            folderName => Error.Failure("scaffold.exists", $"{ModuleFolders.InRoot(folderName)} already exists"),
            () => Result.Success(module));
    }

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
