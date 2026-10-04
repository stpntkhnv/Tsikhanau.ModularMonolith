using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.Infrastructure.EventBus;

public interface IEventBus
{
    Task DeliverAsync(OutboxMessage message, ModuleDbContext dbContext, CancellationToken cancellationToken);
}
