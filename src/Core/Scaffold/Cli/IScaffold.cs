using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal interface IScaffold
{
    Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem);
}
