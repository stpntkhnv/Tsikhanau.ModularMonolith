using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Infrastructure.EventBus;

public interface IEventBus
{
    Task DeliverAsync(OutboxMessage message, ModuleDbContext dbContext, CancellationToken cancellationToken);
}
