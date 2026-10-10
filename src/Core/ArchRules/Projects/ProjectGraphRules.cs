using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class ProjectGraphRules
{
    private static readonly String[] AllowedContractsReferences = ["Tsikhanau.Monolith.Contracts"];

    private static readonly String[] AllowedCoreReferences =
    [
        "Tsikhanau.Monolith.Domain",
        "Tsikhanau.Monolith.Application",
        "Tsikhanau.Monolith.Infrastructure",
        "Tsikhanau.Monolith.Api",
    ];

    public static RuleEvaluation ModuleHasItsProjectAndItsContracts(ArchSolution solution, ArchModule module)
    {
        var moduleFolder = solution.GetModuleFolder(module);
        IReadOnlyList<String> projectPaths =
        [
            .. solution
                .GetProjectPaths(moduleFolder)
                .Select(projectPath => ToRelativePath(moduleFolder, projectPath)),
        ];

        return RuleEvaluation.Create(projectPaths, () => GetLayoutViolations(module, projectPaths));
    }

    public static RuleEvaluation ProjectReferencesFollowTheLayerRules(
        ArchSolution solution,
        ArchModules modules,
        ArchModule module)
    {
        var projectPath = solution.GetProjectPath(module, module.RootNamespace);
        var contractsPath = solution.GetProjectPath(module, module.ContractsName);
        String[] paths = [projectPath, contractsPath];
        IReadOnlyList<String> subject =
        [
            .. paths
                .Where(File.Exists)
                .Select(solution.ToSolutionPath),
        ];

        String[] allowedContractsReferences = [.. AllowedContractsReferences, .. modules.SharedKernelNames];

        String[] allowedModuleReferences =
        [
            module.ContractsName,
            .. AllowedCoreReferences,
            .. modules
                .GetOthers(module)
                .Select(otherModule => otherModule.ContractsName),
            .. modules.SharedKernelNames,
        ];

        return RuleEvaluation.Create(
            subject,
            () =>
            [
                .. GetDisallowedReferences(contractsPath, allowedContractsReferences),
                .. GetDisallowedReferences(projectPath, allowedModuleReferences),
            ]);
    }

    private static IEnumerable<String> GetLayoutViolations(ArchModule module, IReadOnlyList<String> projectPaths)
    {
        String[] expectedPaths = [BuildProjectPath(module.RootNamespace), BuildProjectPath(module.ContractsName)];

        var missingPaths = expectedPaths
            .Except(projectPaths)
            .Select(path => $"{module.Name} has no {path}");

        var strayPaths = projectPaths
            .Except(expectedPaths)
            .Select(path => $"{module.Name} has {path}, which is neither its project nor its contracts");

        return [.. missingPaths, .. strayPaths];
    }

    private static IEnumerable<String> GetDisallowedReferences(String projectPath, String[] allowedNames) =>
        File.Exists(projectPath) ? ReadDisallowedReferences(projectPath, allowedNames) : [];

    private static IEnumerable<String> ReadDisallowedReferences(String projectPath, String[] allowedNames) =>
        ProjectReferences
            .Read(projectPath)
            .Where(referenceName => !allowedNames.Contains(referenceName))
            .Select(referenceName => $"{Path.GetFileNameWithoutExtension(projectPath)} references {referenceName}");

    private static String BuildProjectPath(String projectName) => $"{projectName}/{projectName}.csproj";

    private static String ToRelativePath(String folder, String path) =>
        Path
            .GetRelativePath(folder, path)
            .Replace('\\', '/');
}
