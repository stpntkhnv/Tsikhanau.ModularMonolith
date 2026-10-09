using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class ProjectGraphTests
{
    [Theory]
    [EveryProductionModule]
    public void Module_has_its_project_and_its_contracts(String name)
    {
        Assert.SkipWhen(name == ProductionModules.Placeholder, "There are no production modules");

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

    [Theory]
    [EveryProductionModule]
    public void Project_references_follow_the_layer_rules(String moduleName)
    {
        Assert.SkipWhen(moduleName == ProductionModules.Placeholder, "There are no production modules");

        var otherModuleContractsNames = ArchModules.Production
            .Where(otherModuleName => otherModuleName != moduleName)
            .Select(otherModuleName => $"ModularMonolith.Modules.{otherModuleName}.Contracts")
            .ToList();

        var contractsReferences = ReadProjectReferences(moduleName, $"ModularMonolith.Modules.{moduleName}.Contracts");
        var moduleReferences = ReadProjectReferences(moduleName, $"ModularMonolith.Modules.{moduleName}");

        contractsReferences.ShouldBeSubsetOf(["Tsikhanau.Monolith.Contracts"]);
        moduleReferences.ShouldBeSubsetOf(
            [
                $"ModularMonolith.Modules.{moduleName}.Contracts",
                "Tsikhanau.Monolith.Domain",
                "Tsikhanau.Monolith.Application",
                "Tsikhanau.Monolith.Infrastructure",
                "Tsikhanau.Monolith.Api",
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
