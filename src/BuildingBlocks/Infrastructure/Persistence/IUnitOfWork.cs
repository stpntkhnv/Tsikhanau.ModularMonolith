using ModularMonolith.BuildingBlocks.Application.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public interface IUnitOfWork<TModule>
    where TModule : IModuleFacade
{
    Boolean HasChanges { get; }

    Task<Result<Unit>> CommitAsync(CancellationToken cancellationToken);
}
