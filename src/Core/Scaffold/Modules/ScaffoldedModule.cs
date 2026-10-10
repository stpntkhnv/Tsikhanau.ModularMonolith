using Tsikhanau.Monolith.Scaffold.Apps;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record ScaffoldedModule(AppLayout App, ModuleName ModuleName)
{
    public const String ModulesFolder = "src/Modules";

    public String Name => ModuleName.Name;

    public String Schema => ModuleName.Schema;

    public String Route => ModuleName.Route;

    public String Namespace => $"{App.RootNamespace}.Modules.{Name}";

    public String ContractsNamespace => $"{Namespace}.Contracts";

    public String ProjectName => $"{App.ProjectPrefix}.Modules.{Name}";

    public String ContractsProjectName => $"{ProjectName}.Contracts";

    public String UnitTestsAssembly => $"{App.ProjectPrefix}.UnitTests";

    public String IntegrationTestsAssembly => $"{App.ProjectPrefix}.IntegrationTests";

    public String Folder => $"{ModulesFolder}/{Name}";

    public String ProjectPath => InProject($"{ProjectName}.csproj");

    public String ContractsFolder => $"{Folder}/{ContractsProjectName}";

    public String ContractsProjectPath => InContracts($"{ContractsProjectName}.csproj");

    public IReadOnlyList<String> ProjectPaths => [ProjectPath, ContractsProjectPath];

    public String ModuleClass => $"{Name}Module";

    public String FacadeContract => $"I{Name}Module";

    public String FacadeClass => $"{Name}ModuleFacade";

    public String CommandMarker => $"I{Name}Command";

    public String QueryMarker => $"I{Name}Query";

    public String QueryFacade => $"I{Name}QueryFacade";

    public String DbContext => $"{Name}DbContext";

    public String InProject(String path) => $"{Folder}/{ProjectName}/{path}";

    public String InContracts(String path) => $"{ContractsFolder}/{path}";
}
