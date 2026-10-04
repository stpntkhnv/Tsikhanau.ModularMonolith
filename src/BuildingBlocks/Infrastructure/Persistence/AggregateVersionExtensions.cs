using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class AggregateVersionExtensions
{
    private const String VersionProperty = "Version";
    private const Int32 FirstVersion = 1;

    extension(EntityTypeBuilder builder)
    {
        public void HasVersion() =>
            builder
                .Property<Int32>(VersionProperty)
                .IsConcurrencyToken();
    }

    extension<TAggregate>(EntityEntry<TAggregate> entry)
        where TAggregate : class, IAggregateRoot
    {
        public void BumpVersion()
        {
            var versionEntry = entry.Property<Int32>(VersionProperty);
            if (entry.State == EntityState.Added)
            {
                versionEntry.CurrentValue = FirstVersion;
                return;
            }

            versionEntry.IsModified = true;
            versionEntry.CurrentValue = checked(versionEntry.OriginalValue + 1);
        }
    }
}
