using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal interface IQueuesCommand
{
    Task<Result<Unit>> RunAsync(
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken);
}
