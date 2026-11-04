using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record ModuleProjects(String ProjectPrefix, String ModuleName)
{
    private const String ContractsSuffix = ".Contracts";

    public String Folder => ModuleFolders.InRoot(ModuleName);

    public String ProjectName => $"{ProjectPrefix}.Modules.{ModuleName}";

    public String ProjectFolder => $"{Folder}/{ProjectName}";

    public String ProjectPath => InProject($"{ProjectName}.csproj");

    public String ContractsProjectName => $"{ProjectName}{ContractsSuffix}";

    public String ContractsFolder => $"{Folder}/{ContractsProjectName}";

    public String ContractsProjectPath => InContracts($"{ContractsProjectName}.csproj");

    public IReadOnlyList<String> ProjectPaths => [ProjectPath, ContractsProjectPath];

    public String OwnContractsInclude => $@"..\{ContractsProjectName}\{ContractsProjectName}.csproj";

    public String ContractsIncludeFromNeighbour =>
        $@"..\..\{ModuleName}\{ContractsProjectName}\{ContractsProjectName}.csproj";

    public String ProjectIncludeFromHost => $@"..\..\Modules\{ModuleName}\{ProjectName}\{ProjectName}.csproj";

    public String InProject(String path) => $"{ProjectFolder}/{path}";

    public String InContracts(String path) => $"{ContractsFolder}/{path}";

    public static Optional<String> FindModuleOfContracts(String include, String projectPrefix)
    {
        var projectName = Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
        var modulePrefix = $"{projectPrefix}.Modules.";

        return projectName.Length > modulePrefix.Length + ContractsSuffix.Length
            && projectName.StartsWith(modulePrefix, StringComparison.Ordinal)
            && projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal)
            ? projectName[modulePrefix.Length..^ContractsSuffix.Length]
            : Optional<String>.None();
    }
}
