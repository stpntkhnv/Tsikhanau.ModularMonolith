using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ModuleKeyTypes
{
    public static IReadOnlySet<Type> Keys { get; } = new HashSet<Type>
    {
        typeof(ICleanupTask<>),
        typeof(IInternalCommandScheduler<>),
        typeof(InboxPump<>),
        typeof(InternalCommandRegistry<>),
        typeof(InternalCommandScheduler<>),
        typeof(InternalCommandsPump<>),
        typeof(IOutbox<>),
        typeof(IQueuePump<>),
        typeof(ISqlConnectionFactory<>),
        typeof(IUnitOfWork<>),
        typeof(MessagePump<,>),
        typeof(ModuleCleanup<>),
        typeof(ModuleCleanupProcessor<>),
        typeof(ModuleContext<>),
        typeof(ModuleOutbox<>),
        typeof(ModuleQueueProcessor<>),
        typeof(ModuleQueuePump<>),
        typeof(OutboxPump<>),
        typeof(QueueMessagesCleanupTask<>),
        typeof(SqlConnectionFactory<>),
        typeof(UnitOfWork<>),
    };

    public static IReadOnlySet<Type> Facades { get; } = new HashSet<Type>
    {
        typeof(ModuleDispatcher<>),
        typeof(ModuleFacade<>),
    };
}
