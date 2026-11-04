using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.ProjectFiles;

namespace Tsikhanau.Monolith.ArchRules.Projects.References;

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
        var projectPaths = ToRelativePaths(moduleFolder, solution.GetProjectPaths(moduleFolder));
        var expectedPaths = ToRelativePaths(moduleFolder, solution.GetModuleProjectPaths(module));

        return RuleEvaluation.Create(projectPaths, () => GetLayoutViolations(module, projectPaths, expectedPaths));
    }

    public static RuleEvaluation ProjectReferencesFollowTheLayerRules(
        ArchSolution solution,
        ArchModules modules,
        ArchModule module) =>
        RuleEvaluation.Create(
            GetExistingProjects(solution, module),
            () =>
            [
                .. GetDisallowedReferences(
                    solution.GetContractsProjectPath(module),
                    GetAllowedContractsReferences(modules)),
                .. GetDisallowedReferences(
                    solution.GetProjectPath(module),
                    GetAllowedModuleReferences(modules, module)),
            ]);

    private static IEnumerable<String> GetLayoutViolations(
        ArchModule module,
        IReadOnlyList<String> projectPaths,
        IReadOnlyList<String> expectedPaths)
    {
        var missingPaths = expectedPaths
            .Except(projectPaths)
            .Select(path => $"{module.Name} has no {path}");

        var strayPaths = projectPaths
            .Except(expectedPaths)
            .Select(path => $"{module.Name} has {path}, which is neither its project nor its contracts");

        return [.. missingPaths, .. strayPaths];
    }

    private static IReadOnlyList<String> GetExistingProjects(ArchSolution solution, ArchModule module) =>
    [
        .. solution
            .GetModuleProjectPaths(module)
            .Where(File.Exists)
            .Select(solution.ToSolutionPath),
    ];

    private static String[] GetAllowedContractsReferences(ArchModules modules) =>
        [.. AllowedContractsReferences, .. modules.SharedKernelNames];

    private static String[] GetAllowedModuleReferences(ArchModules modules, ArchModule module) =>
    [
        module.ContractsName,
        .. AllowedCoreReferences,
        .. modules
            .GetOthers(module)
            .Select(otherModule => otherModule.ContractsName),
        .. modules.SharedKernelNames,
    ];

    private static IEnumerable<String> GetDisallowedReferences(String projectPath, String[] allowedNames) =>
        File.Exists(projectPath) ? ReadDisallowedReferences(projectPath, allowedNames) : [];

    private static IEnumerable<String> ReadDisallowedReferences(String projectPath, String[] allowedNames) =>
        ProjectReferences
            .Read(projectPath)
            .Where(referenceName => !allowedNames.Contains(referenceName))
            .Select(referenceName => $"{Path.GetFileNameWithoutExtension(projectPath)} references {referenceName}");

    private static IReadOnlyList<String> ToRelativePaths(String folder, IEnumerable<String> paths) =>
        [.. paths.Select(path => ToRelativePath(folder, path))];

    private static String ToRelativePath(String folder, String path) =>
        Path
            .GetRelativePath(folder, path)
            .Replace('\\', '/');
}
