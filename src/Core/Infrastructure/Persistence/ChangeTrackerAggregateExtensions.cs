using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

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

            var rootsWithChangedParts = GetRootsWithChangedParts(entries)
                .Concat(rootsWithEvents)
                .ToHashSet(ReferenceEqualityComparer.Instance);

            return entries
                .Select(entry => entry.Entity)
                .OfType<IAggregateRoot>()
                .Select(root => changeTracker.Context.Entry(root))
                .Where(rootEntry => IsChanged(rootEntry, rootsWithChangedParts))
                .ToList();
        }
    }

    private static IEnumerable<Object> GetRootsWithChangedParts(IReadOnlyList<EntityEntry> entries)
    {
        var changedParts = entries
            .Where(IsChangedPart)
            .ToList();

        if (changedParts.Count == 0)
        {
            return [];
        }

        var entriesByKey = IndexBySingleKey(entries);
        return changedParts.Select(partEntry => GetRootEntry(partEntry, entries, entriesByKey).Entity);
    }

    private static Boolean IsChanged(EntityEntry<IAggregateRoot> rootEntry, HashSet<Object> rootsWithChangedParts) =>
        rootEntry.State is EntityState.Added or EntityState.Modified
            || (rootEntry.State is EntityState.Unchanged && rootsWithChangedParts.Contains(rootEntry.Entity));

    private static Boolean IsChangedPart(EntityEntry entry) =>
        entry.Metadata.IsOwned()
            && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static Dictionary<(IEntityType EntityType, Object? Key), EntityEntry> IndexBySingleKey(
        IReadOnlyList<EntityEntry> entries)
    {
        var entriesByKey = new Dictionary<(IEntityType EntityType, Object? Key), EntityEntry>();
        entries
            .Where(entry => HasSingleKey(entry.Metadata))
            .ForEach(entry => entriesByKey.TryAdd(GetSingleKey(entry), entry));

        return entriesByKey;
    }

    private static Boolean HasSingleKey(IEntityType entityType) => GetKeyProperties(entityType).Count == 1;

    private static (IEntityType EntityType, Object? Key) GetSingleKey(EntityEntry entry) =>
        (entry.Metadata, entry.CurrentValues[GetKeyProperties(entry.Metadata)[0]]);

    private static IReadOnlyList<IProperty> GetKeyProperties(IEntityType entityType) =>
        entityType.FindPrimaryKey()!.Properties;

    private static EntityEntry GetRootEntry(
        EntityEntry partEntry,
        IReadOnlyList<EntityEntry> entries,
        Dictionary<(IEntityType EntityType, Object? Key), EntityEntry> entriesByKey)
    {
        var ownerEntry = GetOwnerEntry(partEntry, entries, entriesByKey);
        return ownerEntry.Metadata.IsOwned() ? GetRootEntry(ownerEntry, entries, entriesByKey) : ownerEntry;
    }

    private static EntityEntry GetOwnerEntry(
        EntityEntry partEntry,
        IReadOnlyList<EntityEntry> entries,
        Dictionary<(IEntityType EntityType, Object? Key), EntityEntry> entriesByKey)
    {
        var ownership = partEntry.Metadata.FindOwnership()!;
        if (IsOwnedThroughSingleKey(ownership))
        {
            return entriesByKey[(ownership.PrincipalEntityType, GetKeyValue(partEntry, ownership.Properties[0]))];
        }

        var ownerKey = ownership.Properties
            .Select(foreignKeyProperty => GetKeyValue(partEntry, foreignKeyProperty))
            .ToList();

        return entries.First(candidateEntry => IsOwner(candidateEntry, ownership, ownerKey));
    }

    private static Boolean IsOwnedThroughSingleKey(IForeignKey ownership) =>
        ownership.Properties.Count == 1 && ownership.PrincipalKey.IsPrimaryKey();

    private static Object? GetKeyValue(EntityEntry partEntry, IProperty foreignKeyProperty) =>
        partEntry.State is EntityState.Deleted
            ? partEntry.OriginalValues[foreignKeyProperty]
            : partEntry.CurrentValues[foreignKeyProperty];

    private static Boolean IsOwner(
        EntityEntry candidateEntry,
        IForeignKey ownership,
        IReadOnlyList<Object?> ownerKey) =>
        candidateEntry.Metadata == ownership.PrincipalEntityType
            && ownership.PrincipalKey.Properties
                .Select(principalKeyProperty => candidateEntry.CurrentValues[principalKeyProperty])
                .SequenceEqual(ownerKey);
}
