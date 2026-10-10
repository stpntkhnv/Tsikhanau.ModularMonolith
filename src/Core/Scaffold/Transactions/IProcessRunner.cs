using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal interface IProcessRunner
{
    Task<Result<Unit>> RunAsync(ExternalStep step, CancellationToken cancellationToken);
}
