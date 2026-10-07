using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching.Invokers;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;

public sealed class ModuleDispatcher<TModule>(
    IServiceProvider serviceProvider,
    ILogger<ModuleDispatcher<TModule>> logger)
    where TModule : IModuleFacade
{
    private static readonly ConcurrentDictionary<Type, String> ActivityNames = new();

    private static String ModuleName => typeof(TModule).Name;

    private IUnitOfWork<TModule> UnitOfWork => serviceProvider.GetRequiredService<IUnitOfWork<TModule>>();

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull =>
        TraceAsync(command.GetType(), () => HandleAndCommitAsync(command, cancellationToken));

    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull =>
        TraceAsync(query.GetType(), () => HandleAsync(query, cancellationToken));

    private Task<Result<TResult>> HandleAndCommitAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        command is ICommitOnFailure
            ? HandleThenCommitAnywayAsync(command, cancellationToken)
            : HandleAsync(command, cancellationToken)
                .BindAsync(value => CommitAsync(value, cancellationToken));

    private async Task<Result<TResult>> HandleThenCommitAnywayAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
        var handlingResult = await HandleAsync(command, cancellationToken);
        if (handlingResult.IsFailure && !UnitOfWork.HasChanges)
        {
            return handlingResult;
        }

        var commitResult = await UnitOfWork.CommitAsync(cancellationToken);
        return commitResult.IsFailure ? commitResult.Error : handlingResult;
    }

    private Task<Result<TResult>> HandleAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull =>
        CommandInvoker<TResult>
            .Get(command.GetType())
            .InvokeAsync(command, serviceProvider, cancellationToken);

    private Task<Result<TResult>> HandleAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryInvoker<TResult>
            .Get(query.GetType())
            .InvokeAsync(query, serviceProvider, cancellationToken);

    private Task<Result<TResult>> CommitAsync<TResult>(TResult value, CancellationToken cancellationToken)
        where TResult : notnull =>
        UnitOfWork
            .CommitAsync(cancellationToken)
            .MapAsync(_ => value);

    private async Task<Result<TResult>> TraceAsync<TResult>(Type requestType, Func<Task<Result<TResult>>> handle)
        where TResult : notnull
    {
        using var activity = AppDiagnostics.ActivitySource.StartActivity(GetActivityName(requestType));
        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await handle();
        Report(result, requestType, startTimestamp, activity);
        return result;
    }

    private static String GetActivityName(Type requestType) =>
        ActivityNames.GetOrAdd(requestType, static type => $"{ModuleName} {type.Name}");

    private void Report<TResult>(Result<TResult> result, Type requestType, Int64 startTimestamp, Activity? activity)
        where TResult : notnull
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        if (result.IsSuccess)
        {
            DispatcherLog.Succeeded(logger, ModuleName, requestType.Name, elapsedMilliseconds);
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
        LogFailure(result.Error, requestType, elapsedMilliseconds);
    }

    private void LogFailure(Error error, Type requestType, Double elapsedMilliseconds)
    {
        if (error.Kind == ErrorKind.Unexpected)
        {
            DispatcherLog.FailedUnexpectedly(logger, ModuleName, requestType.Name, error.Code, elapsedMilliseconds);
            return;
        }

        DispatcherLog.Failed(logger, ModuleName, requestType.Name, error.Code, elapsedMilliseconds);
    }
}
