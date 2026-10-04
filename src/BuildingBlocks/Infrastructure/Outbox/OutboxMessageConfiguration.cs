using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder) =>
        QueueMessageMapping.Map(builder, OutboxMessage.Table, nameof(OutboxMessage.OccurredOn));
}
