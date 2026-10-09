using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Ids;
using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public abstract class ModuleDbContext(DbContextOptions dbContextOptions) : DbContext(dbContextOptions)
{
    private const String UnownedChildEntityProblem =
        "is not owned by an aggregate; map it with OwnsOne or OwnsMany";

    protected abstract String Schema { get; }

    protected virtual IReadOnlyList<Assembly> SharedKernelAssemblies => [];

    protected virtual void ConfigureModule(ModelBuilder modelBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InternalCommandMessageConfiguration());
        ConfigureModule(modelBuilder);
        ConfigureAggregates(modelBuilder);
        NeverGenerateEntityKeys(modelBuilder);
        EnsureChildEntitiesAreOwned(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var typedIdType in TypedIds)
        {
            configurationBuilder
                .Properties(typedIdType)
                .HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(typedIdType));
        }

        foreach (var (valueObjectType, valueType) in ValueObjects)
        {
            configurationBuilder
                .Properties(valueObjectType)
                .HaveConversion(typeof(ValueObjectConverter<,>).MakeGenericType(valueObjectType, valueType));
        }
    }

    private IEnumerable<Type> MappedTypes =>
        SharedKernelAssemblies
            .Prepend(GetType().Assembly)
            .SelectMany(assembly => assembly.ConcreteTypes);

    private IEnumerable<Type> TypedIds => MappedTypes.Where(type => type.Implements(typeof(ITypedId<>)));

    private IEnumerable<(Type ValueObjectType, Type ValueType)> ValueObjects =>
        MappedTypes.SelectMany(GetValueObjectContracts);

    private static IEnumerable<(Type ValueObjectType, Type ValueType)> GetValueObjectContracts(Type type) =>
        type
            .GetInterfacesBuiltFrom(typeof(IValueObject<,>))
            .Select(contract => (type, contract.GenericTypeArguments[1]));

    private static void ConfigureAggregates(ModelBuilder modelBuilder) =>
        modelBuilder.Model
            .GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Implementing<IAggregateRoot>()
            .ToList()
            .ForEach(aggregateType => ConfigureAggregate(modelBuilder.Entity(aggregateType)));

    private static void ConfigureAggregate(EntityTypeBuilder builder)
    {
        builder.Ignore(nameof(IAggregateRoot.DomainEvents));
        builder.HasVersion();
    }

    private static void NeverGenerateEntityKeys(ModelBuilder modelBuilder) =>
        modelBuilder.Model
            .GetEntityTypes()
            .Where(IsDomainEntity)
            .Select(entityType => entityType.FindProperty(nameof(Entity<>.Id)))
            .OfType<IMutableProperty>()
            .ToList()
            .ForEach(idProperty => idProperty.ValueGenerated = ValueGenerated.Never);

    private static void EnsureChildEntitiesAreOwned(ModelBuilder modelBuilder)
    {
        var problems = modelBuilder.Model
            .GetEntityTypes()
            .Where(IsUnownedChildEntity)
            .Select(entityType => $"Entity {entityType.ClrType.FullName} {UnownedChildEntityProblem}")
            .ToList();

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(String.Join("; ", problems));
        }
    }

    private static Boolean IsDomainEntity(IMutableEntityType entityType) =>
        entityType.ClrType.DerivesFrom(typeof(Entity<>));

    private static Boolean IsUnownedChildEntity(IMutableEntityType entityType) =>
        IsDomainEntity(entityType) && !entityType.ClrType.Implements<IAggregateRoot>() && !entityType.IsOwned();
}
