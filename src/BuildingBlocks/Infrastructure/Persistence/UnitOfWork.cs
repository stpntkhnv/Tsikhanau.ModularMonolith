using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Domain.Events;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching.Invokers;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.Shared.Collections;
using Npgsql;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

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

    public Task<Result<Unit>> CommitAsync(CancellationToken cancellationToken) =>
        DispatchDomainEventsAsync(cancellationToken)
            .TapAsync(BumpVersions)
            .BindAsync(roots => SaveAsync(roots, cancellationToken));

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
