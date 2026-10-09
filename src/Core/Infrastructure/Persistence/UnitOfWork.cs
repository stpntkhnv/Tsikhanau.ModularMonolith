using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public sealed class UnitOfWork<TModule>(
    ModuleContext<TModule> moduleContext,
    IServiceProvider serviceProvider,
    QueueSignals queueSignals)
    : IUnitOfWork<TModule>
    where TModule : IModuleFacade
{
    private const Int32 MaxEventRounds = 10;

    private ModuleDbContext DbContext => moduleContext.DbContext;

    public Boolean HasChanges =>
        DbContext.ChangeTracker.HasChanges()
            || DbContext.ChangeTracker
                .Entries<IAggregateRoot>()
                .Any(entry => entry.Entity.DomainEvents.Count > 0);

    public async Task<Result<Unit>> CommitAsync(CancellationToken cancellationToken)
    {
        DbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            return await DispatchDomainEventsAsync(cancellationToken)
                .TapAsync(_ => DbContext.ChangeTracker.DetectChanges())
                .TapAsync(BumpVersions)
                .BindAsync(roots => SaveAsync(roots, cancellationToken));
        }
        finally
        {
            DbContext.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private async Task<Result<IReadOnlyCollection<IAggregateRoot>>> DispatchDomainEventsAsync(
        CancellationToken cancellationToken)
    {
        var dispatchedEvents = new HashSet<DomainEvent>(ReferenceEqualityComparer.Instance);
        var roots = new HashSet<IAggregateRoot>(ReferenceEqualityComparer.Instance);

        for (var round = 0; ; round++)
        {
            var pendingEvents = GetPendingEvents(dispatchedEvents);
            if (pendingEvents.Count == 0)
            {
                return Result.Success<IReadOnlyCollection<IAggregateRoot>>(roots);
            }

            if (round == MaxEventRounds)
            {
                return UnitOfWorkErrors.EventLoop(MaxEventRounds);
            }

            foreach (var (root, domainEvent) in pendingEvents)
            {
                dispatchedEvents.Add(domainEvent);
                roots.Add(root);
                var dispatchResult = await DispatchAsync(domainEvent, cancellationToken);
                if (dispatchResult.IsFailure)
                {
                    return dispatchResult.Error;
                }
            }
        }
    }

    private List<(IAggregateRoot Root, DomainEvent Event)> GetPendingEvents(HashSet<DomainEvent> dispatchedEvents) =>
        DbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .SelectMany(entry => GetEventsWithRoot(entry.Entity))
            .Where(eventWithRoot => !dispatchedEvents.Contains(eventWithRoot.Event))
            .ToList();

    private static IEnumerable<(IAggregateRoot Root, DomainEvent Event)> GetEventsWithRoot(IAggregateRoot root) =>
        root.DomainEvents.Select(domainEvent => (root, domainEvent));

    private Task<Result<Unit>> DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken) =>
        DomainEventInvoker
            .Get(domainEvent.GetType())
            .InvokeAsync(domainEvent, serviceProvider, cancellationToken);

    private void BumpVersions(IReadOnlyCollection<IAggregateRoot> rootsWithEvents) =>
        DbContext.ChangeTracker
            .GetChangedAggregateEntries(rootsWithEvents)
            .ForEach(entry => entry.BumpVersion());

    private Task<Result<Unit>> SaveAsync(IReadOnlyCollection<IAggregateRoot> roots, CancellationToken cancellationToken)
    {
        var hasNewQueueMessages = HasNewQueueMessages();
        return TrySaveChangesAsync(cancellationToken)
            .TapAsync(_ => roots.ForEach(root => root.ClearDomainEvents()))
            .TapAsync(_ => WakeQueuesIf(hasNewQueueMessages));
    }

    private async Task<Result<Unit>> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
        catch (DbUpdateConcurrencyException)
        {
            return UnitOfWorkErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (FindConstraintError(exception) is { HasValue: true } constraintError)
        {
            return constraintError.Value;
        }
    }

    private Boolean HasNewQueueMessages() =>
        DbContext.ChangeTracker
            .Entries()
            .Any(entry => entry.State == EntityState.Added && entry.Entity is OutboxMessage or InternalCommandMessage);

    private void WakeQueuesIf(Boolean hasNewQueueMessages)
    {
        if (hasNewQueueMessages)
        {
            queueSignals.Signal(typeof(TModule));
        }
    }

    private Optional<Error> FindConstraintError(DbUpdateException exception) =>
        FindViolatedUniqueConstraint(exception)
            .Bind(FindKnownConstraintError);

    private static Optional<String> FindViolatedUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            ? violation.ConstraintName
            : Optional<String>.None();

    private Optional<Error> FindKnownConstraintError(String constraintName) =>
        moduleContext.Descriptor.ConstraintErrors.GetValueOrDefault(constraintName);
}
