using ModularMonolith.ArchTests.Support;
using ModularMonolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class ProjectGraphTests
{
    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Module_has_its_project_and_its_contracts(String name)
    {
        var moduleFolder = Path.Combine(SolutionPaths.Modules, name);
        var projectPaths = Directory
            .GetFiles(moduleFolder, "*.csproj", SearchOption.AllDirectories)
            .Select(path => ToRelativePath(moduleFolder, path));

        var contractsName = $"ModularMonolith.Modules.{name}.Contracts";

        projectPaths.ShouldBe(
            [
                $"ModularMonolith.Modules.{name}/ModularMonolith.Modules.{name}.csproj",
                $"{contractsName}/{contractsName}.csproj",
            ],
            ignoreOrder: true);
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Project_references_follow_the_layer_rules(String moduleName)
    {
        var otherModuleContractsNames = ArchModules.Production
            .Where(otherModuleName => otherModuleName != moduleName)
            .Select(otherModuleName => $"ModularMonolith.Modules.{otherModuleName}.Contracts")
            .ToList();

        var contractsReferences = ReadProjectReferences(moduleName, $"ModularMonolith.Modules.{moduleName}.Contracts");
        var moduleReferences = ReadProjectReferences(moduleName, $"ModularMonolith.Modules.{moduleName}");

        contractsReferences.ShouldBeSubsetOf(["ModularMonolith.BuildingBlocks.Contracts"]);
        moduleReferences.ShouldBeSubsetOf(
            [
                $"ModularMonolith.Modules.{moduleName}.Contracts",
                "ModularMonolith.BuildingBlocks.Domain",
                "ModularMonolith.BuildingBlocks.Application",
                "ModularMonolith.BuildingBlocks.Infrastructure",
                "ModularMonolith.BuildingBlocks.Api",
                .. otherModuleContractsNames,
            ]);
    }

    private static String ToRelativePath(String folder, String path)
    {
        var relativePath = Path.GetRelativePath(folder, path);

        return relativePath.Replace('\\', '/');
    }

    private static IReadOnlyList<String> ReadProjectReferences(String moduleName, String projectName) =>
        ProjectReferences.Read(Path.Combine(SolutionPaths.Modules, moduleName, projectName, $"{projectName}.csproj"));
}
