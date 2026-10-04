using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

internal static class ChangeTrackerAggregateExtensions
{
    extension(ChangeTracker changeTracker)
    {
        public IReadOnlyList<EntityEntry<IAggregateRoot>> GetChangedAggregateEntries(
            IReadOnlyCollection<IAggregateRoot> rootsWithEvents)
        {
            var entries = changeTracker
                .Entries()
                .ToList();

            var rootsWithChangedParts = new HashSet<Object>(
                entries
                    .Where(IsChangedPart)
                    .Select(partEntry => GetRootEntry(partEntry, entries).Entity)
                    .Concat(rootsWithEvents),
                ReferenceEqualityComparer.Instance);

            return entries
                .Select(entry => entry.Entity)
                .OfType<IAggregateRoot>()
                .Select(root => changeTracker.Context.Entry(root))
                .Where(rootEntry => IsChanged(rootEntry, rootsWithChangedParts))
                .ToList();
        }
    }

    private static Boolean IsChanged(EntityEntry<IAggregateRoot> rootEntry, HashSet<Object> rootsWithChangedParts) =>
        rootEntry.State is EntityState.Added or EntityState.Modified
            || (rootEntry.State is EntityState.Unchanged && rootsWithChangedParts.Contains(rootEntry.Entity));

    private static Boolean IsChangedPart(EntityEntry entry) =>
        entry.Metadata.IsOwned()
            && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static EntityEntry GetRootEntry(EntityEntry partEntry, IReadOnlyList<EntityEntry> entries)
    {
        var ownerEntry = GetOwnerEntry(partEntry, entries);
        return ownerEntry.Metadata.IsOwned() ? GetRootEntry(ownerEntry, entries) : ownerEntry;
    }

    private static EntityEntry GetOwnerEntry(EntityEntry partEntry, IReadOnlyList<EntityEntry> entries)
    {
        var ownership = partEntry.Metadata.FindOwnership()!;
        var ownerKey = ownership.Properties
            .Select(foreignKeyProperty => GetKeyValue(partEntry.Property(foreignKeyProperty.Name), partEntry.State))
            .ToList();

        return entries.First(candidateEntry => IsOwner(candidateEntry, ownership, ownerKey));
    }

    private static Object? GetKeyValue(PropertyEntry propertyEntry, EntityState state) =>
        state is EntityState.Deleted ? propertyEntry.OriginalValue : propertyEntry.CurrentValue;

    private static Boolean IsOwner(
        EntityEntry candidateEntry,
        IForeignKey ownership,
        IReadOnlyList<Object?> ownerKey) =>
        candidateEntry.Metadata == ownership.PrincipalEntityType
            && ownership.PrincipalKey.Properties
                .Select(principalKeyProperty => candidateEntry.Property(principalKeyProperty.Name).CurrentValue)
                .SequenceEqual(ownerKey);
}
