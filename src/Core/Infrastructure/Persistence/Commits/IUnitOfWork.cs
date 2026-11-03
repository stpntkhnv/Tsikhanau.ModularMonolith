using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Persistence.Commits;

public interface IUnitOfWork<TModule>
    where TModule : IModuleFacade
{
    Boolean HasChanges { get; }

    Task<Result<Unit>> CommitAsync(CancellationToken cancellationToken);
}
