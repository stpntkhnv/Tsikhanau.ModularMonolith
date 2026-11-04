using Microsoft.EntityFrameworkCore;

namespace Tsikhanau.Monolith.Infrastructure.Queues.Messages;

internal static class QueueMessageMapping
{
    private const String VacuumScaleFactor = "autovacuum_vacuum_scale_factor";
    private const String VacuumThreshold = "autovacuum_vacuum_threshold";
    private const Double IgnoreTableSize = 0;
    private const Int32 DeadRowsBeforeVacuum = 10_000;

    public static void Map<TMessage>(ModelBuilder modelBuilder, String table, String orderProperty)
        where TMessage : QueueMessage
    {
        var builder = modelBuilder.Entity<TMessage>();
        builder.ToTable(table);
        builder.HasKey(message => message.Id);

        builder
            .HasStorageParameter(VacuumScaleFactor, IgnoreTableSize)
            .HasStorageParameter(VacuumThreshold, DeadRowsBeforeVacuum);

        builder
            .Property(message => message.Id)
            .ValueGeneratedNever();

        builder
            .Property(message => message.Payload)
            .HasColumnType("jsonb");

        builder
            .Property(message => message.ContainsSecrets)
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder
            .HasIndex(nameof(QueueMessage.NextAttemptOn), orderProperty, nameof(QueueMessage.Id))
            .HasFilter("processed_on IS NULL AND failed_on IS NULL");

        builder
            .HasIndex(nameof(QueueMessage.ProcessedOn))
            .HasFilter("processed_on IS NOT NULL");

        builder
            .HasIndex(nameof(QueueMessage.FailedOn))
            .HasFilter("failed_on IS NOT NULL");
    }
}
