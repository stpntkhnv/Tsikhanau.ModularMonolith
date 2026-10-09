using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public interface ICleanupTask<TModule>
    where TModule : IModuleFacade
{
    Task<Int32> DeleteExpiredAsync(DateTimeOffset deleteBefore, CancellationToken cancellationToken);
}
