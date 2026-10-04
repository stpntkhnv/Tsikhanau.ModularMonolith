namespace ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;

public interface IModuleCleanup
{
    Task<Int32> RunOnceAsync(CancellationToken cancellationToken);
}
