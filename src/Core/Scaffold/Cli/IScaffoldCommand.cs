using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal interface IScaffoldCommand
{
    Result<ScaffoldPlan> Plan(AppLayout app);
}
