using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Domain.Ids;
using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public abstract class ModuleDbContext(DbContextOptions dbContextOptions) : DbContext(dbContextOptions)
{
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

    private static Boolean IsDomainEntity(IMutableEntityType entityType) =>
        entityType.ClrType.DerivesFrom(typeof(Entity<>));
}
