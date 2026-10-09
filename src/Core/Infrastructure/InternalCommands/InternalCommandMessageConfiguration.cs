using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

internal sealed class InternalCommandMessageConfiguration : IEntityTypeConfiguration<InternalCommandMessage>
{
    public void Configure(EntityTypeBuilder<InternalCommandMessage> builder) =>
        QueueMessageMapping.Map(builder, InternalCommandMessage.Table, nameof(InternalCommandMessage.EnqueuedOn));
}
