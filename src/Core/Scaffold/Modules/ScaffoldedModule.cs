using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Names;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record ScaffoldedModule(AppLayout App, ModuleName ModuleName)
{
    public String Name => ModuleName.Name;

    public String Schema => ModuleName.Schema;

    public String Route => ModuleName.Route;

    public ModuleProjects Projects => new(App.ProjectPrefix, Name);

    public String Namespace => $"{App.RootNamespace}.Modules.{Name}";

    public String ContractsNamespace => $"{Namespace}.Contracts";

    public String UnitTestsAssembly => $"{App.ProjectPrefix}.UnitTests";

    public String IntegrationTestsAssembly => $"{App.ProjectPrefix}.IntegrationTests";

    public String ModuleClass => $"{Name}Module";

    public String FacadeContract => $"I{Name}Module";

    public String FacadeClass => $"{Name}ModuleFacade";

    public String CommandMarker => $"I{Name}Command";

    public String QueryMarker => $"I{Name}Query";

    public String QueryFacade => $"I{Name}QueryFacade";

    public String DbContext => $"{Name}DbContext";

    public static Result<ScaffoldedModule> Parse(AppLayout app, String name) =>
        ModuleName
            .Parse(name, app.DatabaseName)
            .Map(moduleName => new ScaffoldedModule(app, moduleName));
}
