using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Domain.Aggregates;

namespace Tsikhanau.Monolith.Infrastructure.Persistence.Commits;

public static class AggregateVersionExtensions
{
    private const String VersionProperty = "Version";
    private const Int32 FirstVersion = 1;

    public static void HasVersion(this EntityTypeBuilder builder) =>
        builder
            .Property<Int32>(VersionProperty)
            .IsConcurrencyToken();

    public static void BumpVersion<TAggregate>(this EntityEntry<TAggregate> entry)
        where TAggregate : class, IAggregateRoot
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
