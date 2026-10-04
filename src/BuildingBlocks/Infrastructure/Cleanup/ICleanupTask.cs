using ModularMonolith.BuildingBlocks.Application.Modules;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;

public interface ICleanupTask<TModule>
    where TModule : IModuleFacade
{
    Task<Int32> DeleteExpiredAsync(DateTimeOffset deleteBefore, CancellationToken cancellationToken);
}
