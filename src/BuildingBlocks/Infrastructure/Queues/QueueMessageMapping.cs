using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

internal static class QueueMessageMapping
{
    public static void Map<TMessage>(EntityTypeBuilder<TMessage> builder, String table, String orderProperty)
        where TMessage : QueueMessage
    {
        builder.ToTable(table);
        builder.HasKey(message => message.Id);

        builder
            .Property(message => message.Id)
            .ValueGeneratedNever();

        builder
            .Property(message => message.Payload)
            .HasColumnType("jsonb");

        builder
            .HasIndex(nameof(QueueMessage.NextAttemptOn), orderProperty, nameof(QueueMessage.Id))
            .HasFilter("processed_on IS NULL AND failed_on IS NULL");
    }
}
