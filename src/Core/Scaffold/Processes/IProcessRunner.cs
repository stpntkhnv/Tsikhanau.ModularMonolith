using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Processes;

internal interface IProcessRunner
{
    Task<Result<Unit>> RunAsync(ExternalStep step, CancellationToken cancellationToken);
}
