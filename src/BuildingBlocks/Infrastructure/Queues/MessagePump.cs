using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

public abstract class MessagePump<TModule, TMessage>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    TimeProvider timeProvider,
    ILogger logger)
    where TModule : IModuleFacade
    where TMessage : QueueMessage
{
    private static readonly TimeSpan PastEndedLeases = TimeSpan.FromMicroseconds(1);

    private static readonly TimeSpan ShortCommandTimeout = TimeSpan.FromSeconds(5);

    protected abstract String Table { get; }

    protected abstract String OrderColumn { get; }

    private static String ModuleName => typeof(TModule).Name;

    private TimeSpan HandlerTimeout => options.Value.HandlerTimeout;

    private TimeSpan Lease => HandlerTimeout * 2;

    public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
    {
        var processedCount = 0;
        while (await TryProcessNextAsync(cancellationToken))
        {
            processedCount++;
        }

        return processedCount;
    }

    public async Task<Boolean> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        var claimedMessage = await ClaimAsync(cancellationToken);
        if (claimedMessage.IsNone)
        {
            return false;
        }

        var message = claimedMessage.Value;
        using var activity = StartActivity(message);
        using var logScope = PumpLog.BeginMessageScope(logger, ModuleName, Table, message.Id, message.Type);
        await ProcessAsync(message, cancellationToken);
        return true;
    }

    protected abstract Task<Result<Unit>> HandleAsync(
        IServiceProvider serviceProvider,
        ModuleDbContext dbContext,
        TMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken);

    protected virtual void OnCommitted(TMessage message) { }

    protected virtual Boolean RunsOutsideTransaction(TMessage message) => false;

    protected virtual Task<Result<Unit>> HandleOutsideTransactionAsync(
        IServiceProvider serviceProvider,
        TMessage message,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{GetType().Name} handles every message in a transaction");

    private async Task ProcessAsync(TMessage message, CancellationToken cancellationToken)
    {
        if (RetryPolicy.HasRunOut(message.Attempts))
        {
            await RecordFailureAsync(message, HandlingFailure.Abandoned(message.Attempts));
            return;
        }

        try
        {
            var failure = await HandleWithinDeadlineAsync(message, cancellationToken);
            if (failure.HasValue)
            {
                await RecordFailureAsync(message, failure.Value);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            await GiveBackAsync(message);
            throw;
        }
    }

    private async Task<Optional<HandlingFailure>> HandleWithinDeadlineAsync(
        TMessage message,
        CancellationToken cancellationToken)
    {
        using var deadline = HandlerDeadline.Start(HandlerTimeout, timeProvider, cancellationToken);
        try
        {
            return RunsOutsideTransaction(message)
                ? await HandleOutsideTransactionAndMarkAsync(message, deadline.Token)
                : await HandleInTransactionAsync(message, deadline.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return deadline.HasPassed
                ? HandlingFailure.TimedOut(HandlerTimeout, exception)
                : HandlingFailure.From(exception);
        }
    }

    private async Task<Optional<HandlingFailure>> HandleInTransactionAsync(
        TMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var moduleContext = scope.ServiceProvider.GetRequiredService<ModuleContext<TModule>>();
        var dbContext = moduleContext.DbContext;
        dbContext.Database.AutoSavepointsEnabled = false;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(BuildTransactionLimitsSql(), cancellationToken);
        var lockedMessage = await LockAsync(moduleContext, message, cancellationToken);
        if (lockedMessage.IsNone)
        {
            return Optional<HandlingFailure>.None();
        }

        var processedOn = timeProvider.GetUtcNow();
        var outcome = await HandleAsync(
            scope.ServiceProvider,
            dbContext,
            lockedMessage.Value,
            processedOn,
            cancellationToken);

        if (outcome.IsFailure)
        {
            return HandlingFailure.From(outcome.Error);
        }

        await transaction.CommitAsync(cancellationToken);
        OnCommitted(lockedMessage.Value);
        return Optional<HandlingFailure>.None();
    }

    private async Task<Optional<HandlingFailure>> HandleOutsideTransactionAndMarkAsync(
        TMessage message,
        CancellationToken cancellationToken)
    {
        await using (var scope = serviceScopeFactory.CreateAsyncScope())
        {
            var outcome = await HandleOutsideTransactionAsync(scope.ServiceProvider, message, cancellationToken);
            if (scope.ServiceProvider.GetRequiredService<IUnitOfWork<TModule>>().HasChanges)
            {
                return HandlingFailure.ChangedStateOutsideTransaction;
            }

            if (outcome.IsFailure)
            {
                return HandlingFailure.From(outcome.Error);
            }
        }

        await MarkProcessedAsync(message, timeProvider.GetUtcNow());
        OnCommitted(message);
        return Optional<HandlingFailure>.None();
    }

    private async Task<Optional<TMessage>> ClaimAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var moduleContext = scope.ServiceProvider.GetRequiredService<ModuleContext<TModule>>();
        moduleContext.DbContext.Database.SetCommandTimeout(ShortCommandTimeout);
        var claimedOn = timeProvider.GetUtcNow();
        var rows = await moduleContext.DbContext
            .Set<TMessage>()
            .FromSqlRaw(BuildClaimSql(moduleContext.Descriptor.Schema), claimedOn, claimedOn + Lease)
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        return rows.FirstOrDefault();
    }

    private String BuildClaimSql(String schema) =>
        $$"""
        UPDATE "{{schema}}".{{Table}} AS claimed
        SET attempts = claimed.attempts + 1, next_attempt_on = {1}
        WHERE claimed.id = (
            SELECT id FROM "{{schema}}".{{Table}}
            WHERE processed_on IS NULL AND failed_on IS NULL AND next_attempt_on <= {0}
            ORDER BY next_attempt_on, {{OrderColumn}}, id
            LIMIT 1
            FOR UPDATE SKIP LOCKED)
        RETURNING claimed.*
        """;

    private async Task<Optional<TMessage>> LockAsync(
        ModuleContext<TModule> moduleContext,
        TMessage message,
        CancellationToken cancellationToken)
    {
        var rows = await moduleContext.DbContext
            .Set<TMessage>()
            .FromSqlRaw(
                BuildLockSql(moduleContext.Descriptor.Schema),
                message.Id,
                message.Attempts,
                message.NextAttemptOn)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    private String BuildLockSql(String schema) =>
        $$"""
        SELECT * FROM "{{schema}}".{{Table}}
        WHERE id = {0} AND attempts = {1} AND next_attempt_on = {2} AND processed_on IS NULL AND failed_on IS NULL
        FOR UPDATE
        """;

    private String BuildTransactionLimitsSql()
    {
        var milliseconds = ((Int64)HandlerTimeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        return $"SET LOCAL transaction_timeout = {milliseconds}";
    }

    private Task RecordFailureAsync(TMessage message, HandlingFailure failure)
    {
        var decision = RetryPolicy.Decide(message.Attempts, failure, timeProvider.GetUtcNow());
        LogFailure(failure, decision);
        return UpdateClaimedRowAsync(
            message,
            setters => setters
                .SetProperty(candidate => candidate.LastError, failure.Reason)
                .SetProperty(candidate => candidate.NextAttemptOn, decision.NextAttemptOn)
                .SetProperty(candidate => candidate.FailedOn, decision.FailedOn));
    }

    private Task MarkProcessedAsync(TMessage message, DateTimeOffset processedOn) =>
        UpdateClaimedRowAsync(
            message,
            setters => setters
                .SetProperty(candidate => candidate.ProcessedOn, processedOn)
                .SetProperty(candidate => candidate.Payload, message.Payload));

    private Task GiveBackAsync(TMessage message) =>
        UpdateClaimedRowAsync(
            message,
            setters => setters
                .SetProperty(candidate => candidate.Attempts, message.Attempts - 1)
                .SetProperty(candidate => candidate.NextAttemptOn, CalculateGivenBackDueOn()));

    private DateTimeOffset CalculateGivenBackDueOn() => timeProvider.GetUtcNow() + PastEndedLeases;

    private async Task UpdateClaimedRowAsync(TMessage message, Action<UpdateSettersBuilder<TMessage>> setPropertyCalls)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var moduleContext = scope.ServiceProvider.GetRequiredService<ModuleContext<TModule>>();
        moduleContext.DbContext.Database.SetCommandTimeout(ShortCommandTimeout);
        await moduleContext.DbContext
            .Set<TMessage>()
            .Where(UntouchedSince(message))
            .ExecuteUpdateAsync(setPropertyCalls, CancellationToken.None);
    }

    private static Expression<Func<TMessage, Boolean>> UntouchedSince(TMessage message) =>
        candidate => candidate.Id == message.Id
            && candidate.Attempts == message.Attempts
            && candidate.NextAttemptOn == message.NextAttemptOn
            && candidate.ProcessedOn == null
            && candidate.FailedOn == null;

    private void LogFailure(HandlingFailure failure, RetryDecision decision)
    {
        if (!decision.HasGivenUp)
        {
            PumpLog.Retrying(logger, failure.Reason, failure.Exception);
            return;
        }

        if (failure.IsPermanent)
        {
            PumpLog.Failed(logger, failure.Reason, failure.Exception);
            return;
        }

        PumpLog.GaveUp(logger, decision.Attempts, failure.Reason, failure.Exception);
    }

    private Activity? StartActivity(TMessage message)
    {
        var name = $"{ModuleName} {Table} {message.Type}";
        return ActivityContext.TryParse(message.TraceParent, null, out var parentContext)
            ? AppDiagnostics.ActivitySource.StartActivity(name, ActivityKind.Consumer, parentContext)
            : AppDiagnostics.ActivitySource.StartActivity(name, ActivityKind.Consumer);
    }
}
