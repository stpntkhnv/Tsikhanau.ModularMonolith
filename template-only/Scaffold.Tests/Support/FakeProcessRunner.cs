using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal sealed class FakeProcessRunner(Func<ExternalStep, CancellationToken, Task<Result<Unit>>> run)
    : IProcessRunner
{
    public List<String> Steps { get; } = [];

    public static FakeProcessRunner Succeeding() => new((_, _) => Task.FromResult(Result.Success()));

    public Task<Result<Unit>> RunAsync(ExternalStep step, CancellationToken cancellationToken)
    {
        Steps.Add(step.ToString());
        return run(step, cancellationToken);
    }
}
