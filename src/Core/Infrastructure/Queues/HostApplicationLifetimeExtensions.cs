using Microsoft.Extensions.Hosting;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal static class HostApplicationLifetimeExtensions
{
    extension(IHostApplicationLifetime hostApplicationLifetime)
    {
        public Boolean IsStopping(CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested
            || hostApplicationLifetime.ApplicationStopping.IsCancellationRequested;
    }
}
