using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Ids;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Persistence;

public sealed class ModelTests
{
    [Fact]
    public void Aggregate_key_is_never_generated_and_uses_the_typed_id_converter()
    {
        using var dbContext = CreatePingsDbContext();

        var idProperty = dbContext.Model
            .FindEntityType(typeof(Ping))!
            .FindProperty(nameof(Ping.Id))!;

        var converter = idProperty.GetValueConverter();

        idProperty.ValueGenerated.ShouldBe(ValueGenerated.Never);
        converter.ShouldBeOfType<TypedIdConverter<PingId>>();
    }

    [Fact]
    public void Owned_child_key_is_never_generated_and_uses_the_typed_id_converter()
    {
        using var dbContext = CreatePingsDbContext();

        var idProperty = dbContext.Model
            .FindEntityType(typeof(Echo))!
            .FindProperty(nameof(Echo.Id))!;

        var converter = idProperty.GetValueConverter();

        idProperty.ValueGenerated.ShouldBe(ValueGenerated.Never);
        converter.ShouldBeOfType<TypedIdConverter<EchoId>>();
    }

    [Fact]
    public void Bumping_an_unchanged_aggregate_marks_only_its_version()
    {
        using var dbContext = CreatePingsDbContext();
        var label = PingLabel.Create("p", "label").Value;
        var ping = Ping.Create(PingId.New(DateTimeOffset.UnixEpoch), label, DateTimeOffset.UnixEpoch);
        var pingEntry = dbContext.Attach(ping);

        pingEntry.BumpVersion();

        var modifiedPropertyNames = pingEntry.Properties
            .Where(propertyEntry => propertyEntry.IsModified)
            .Select(propertyEntry => propertyEntry.Metadata.Name)
            .ToList();

        var versionEntry = pingEntry.Property<Int32>("Version");

        modifiedPropertyNames.ShouldBe(["Version"]);
        versionEntry.OriginalValue.ShouldBe(0);
        versionEntry.CurrentValue.ShouldBe(1);
    }

    [Fact]
    public void Optional_property_is_ignored_and_its_field_is_mapped_to_a_snake_case_column()
    {
        using var dbContext = CreatePingsDbContext();
        var pingEntityType = dbContext.Model.FindEntityType(typeof(Ping))!;

        var optionalProperty = pingEntityType.FindProperty(nameof(Ping.ArchivedOn));
        var fieldProperty = pingEntityType.FindProperty("_archivedOn")!;

        var columnName = fieldProperty.GetColumnName();

        optionalProperty.ShouldBeNull();
        columnName.ShouldBe("archived_on");
        fieldProperty.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void Child_entity_that_is_not_owned_fails_the_model()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<BasketsDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, "Host=localhost", BasketsDbContext.SchemaName);
        using var dbContext = new BasketsDbContext(dbContextOptionsBuilder.Options);

        var exception = Should.Throw<InvalidOperationException>(() => dbContext.Model);

        exception.Message.ShouldBe(
            $"Entity {typeof(BasketItem).FullName} is not owned by an aggregate; map it with OwnsOne or OwnsMany");
    }

    private static PingsDbContext CreatePingsDbContext()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<PingsDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, "Host=localhost", PingsDbContext.SchemaName);
        return new PingsDbContext(dbContextOptionsBuilder.Options);
    }

    private readonly record struct BasketId(Guid Value) : ITypedId<BasketId>
    {
        public static BasketId From(Guid value) => new(value);
    }

    private readonly record struct BasketItemId(Guid Value) : ITypedId<BasketItemId>
    {
        public static BasketItemId From(Guid value) => new(value);
    }

    private sealed class Basket : AggregateRoot<BasketId>
    {
        private readonly List<BasketItem> _items = [];

        private Basket(BasketId id) : base(id) { }

        public IReadOnlyList<BasketItem> Items => _items;
    }

    private sealed class BasketItem : Entity<BasketItemId>
    {
        private BasketItem(BasketItemId id) : base(id) { }
    }

    private sealed class BasketsDbContext(DbContextOptions<BasketsDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "baskets";

        protected override String Schema => SchemaName;

        protected override void ConfigureModule(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<Basket>()
                .HasKey(basket => basket.Id);

            modelBuilder
                .Entity<BasketItem>()
                .HasKey(basketItem => basketItem.Id);

            modelBuilder
                .Entity<Basket>()
                .HasMany(basket => basket.Items)
                .WithOne();
        }
    }
}
