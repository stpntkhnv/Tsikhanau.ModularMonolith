namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public interface IModuleCleanup
{
    Task<Int32> RunOnceAsync(CancellationToken cancellationToken);
}
