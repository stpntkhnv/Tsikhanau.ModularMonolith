using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

internal sealed class InternalCommandMessageConfiguration : IEntityTypeConfiguration<InternalCommandMessage>
{
    public void Configure(EntityTypeBuilder<InternalCommandMessage> builder) =>
        QueueMessageMapping.Map(builder, InternalCommandMessage.Table, nameof(InternalCommandMessage.EnqueuedOn));
}
