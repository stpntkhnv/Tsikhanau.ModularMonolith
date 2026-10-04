using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;

public abstract class ModuleFacade<TModule>(IServiceScopeFactory serviceScopeFactory)
    where TModule : IModuleFacade
{
    private const Int32 AttemptsOnConflict = 3;

    protected async Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
        var result = await SendInFreshScopeAsync(command, cancellationToken);
        for (var attempt = 1; attempt < GetMaxAttempts(command) && IsConcurrencyConflict(result); attempt++)
        {
            result = await SendInFreshScopeAsync(command, cancellationToken);
        }

        return result;
    }

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

    private static Int32 GetMaxAttempts<TResult>(ICommand<TResult> command)
        where TResult : notnull =>
        command is IRetryOnConflict ? AttemptsOnConflict : 1;

    private static Boolean IsConcurrencyConflict<TResult>(Result<TResult> result)
        where TResult : notnull =>
        result.IsFailure && result.Error == UnitOfWorkErrors.ConcurrencyConflict;
}
