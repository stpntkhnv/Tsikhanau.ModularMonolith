using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Infrastructure.Persistence.Commits;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Dispatching;

public abstract class ModuleFacade<TModule>(IServiceScopeFactory serviceScopeFactory)
    where TModule : IModuleFacade
{
    private const Int32 AttemptsOnConflict = 3;

    protected Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        command is IRetryOnConflict
            ? SendRetryingOnConflictAsync(command, cancellationToken)
            : SendInFreshScopeAsync(command, cancellationToken);

    protected async Task<Result<TResult>> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<TModule>>()
            .QueryAsync(query, cancellationToken);
    }

    private async Task<Result<TResult>> SendRetryingOnConflictAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
        var result = await SendInFreshScopeAsync(command, cancellationToken);
        for (var attempt = 1; attempt < AttemptsOnConflict && IsConcurrencyConflict(result); attempt++)
        {
            result = await SendInFreshScopeAsync(command, cancellationToken);
        }

        return result;
    }

    private async Task<Result<TResult>> SendInFreshScopeAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<TModule>>()
            .SendAsync(command, cancellationToken);
    }

    private static Boolean IsConcurrencyConflict<TResult>(Result<TResult> result)
        where TResult : notnull =>
        result.IsFailure && UnitOfWorkErrors.IsConcurrencyConflict(result.Error);
}
