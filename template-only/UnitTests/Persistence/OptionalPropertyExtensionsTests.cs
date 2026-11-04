using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Domain.Optionals;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Persistence;

public sealed class OptionalPropertyExtensionsTests
{
    private const String EmptyColumnNameProblem =
        "gets an empty column name, since the naming convention drops letters without upper and lower case: "
        + "give the property a name with Latin letters and keep its old column with HasColumnName after HasOptional";

    [Theory]
    [InlineData(nameof(PlainShipment.Line2Address), "_line2Address")]
    [InlineData(nameof(PlainShipment.Sha512Hash), "_sha512Hash")]
    [InlineData(nameof(PlainShipment.ArchivedOn), "_archivedOn")]
    [InlineData(nameof(PlainShipment.Label标签), "_label标签")]
    public void Optional_property_gets_the_column_a_plain_property_of_the_same_name_gets(
        String propertyName,
        String fieldName)
    {
        using var dbContext = CreateShipmentsDbContext();

        var optionalColumn = GetColumnName<OptionalShipment>(dbContext.Model, fieldName);
        var plainColumn = GetColumnName<PlainShipment>(dbContext.Model, propertyName);

        optionalColumn.ShouldBe(plainColumn);
    }

    [Fact]
    public void Optional_property_named_only_with_letters_the_convention_drops_stops_with_a_message_that_names_it()
    {
        var builder = new ModelBuilder().Entity<TaggedShipment>();

        var exception = Should.Throw<InvalidOperationException>(() => builder.HasOptional(shipment => shipment.标签));

        exception.Message.ShouldBe($"Optional property {typeof(TaggedShipment).FullName}.标签 {EmptyColumnNameProblem}");
    }

    [Fact]
    public void Optional_column_that_takes_the_name_of_a_plain_column_moves_the_plain_column_to_a_numbered_name()
    {
        using var dbContext = CreateShipmentsDbContext();

        var optionalColumn = GetColumnName<LabelShipment>(dbContext.Model, "_label标签");
        var plainColumn = GetColumnName<LabelShipment>(dbContext.Model, nameof(LabelShipment.Label));

        optionalColumn.ShouldBe("label");
        plainColumn.ShouldBe("label1");
    }

    [Fact]
    public void Optional_column_pinned_to_its_old_name_leaves_the_plain_column_its_own_name()
    {
        using var dbContext = CreateShipmentsDbContext();

        var optionalColumn = GetColumnName<PinnedLabelShipment>(dbContext.Model, "_label标签");
        var plainColumn = GetColumnName<PinnedLabelShipment>(dbContext.Model, nameof(PinnedLabelShipment.Label));

        optionalColumn.ShouldBe("label标签");
        plainColumn.ShouldBe("label");
    }

    private static ShipmentsDbContext CreateShipmentsDbContext()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<ShipmentsDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, "Host=localhost", ShipmentsDbContext.SchemaName);
        return new ShipmentsDbContext(dbContextOptionsBuilder.Options);
    }

    private static String GetColumnName<TEntity>(IModel model, String propertyName) =>
        model
            .FindEntityType(typeof(TEntity))
            .ShouldNotBeNull()
            .FindProperty(propertyName)
            .ShouldNotBeNull()
            .GetTableColumnMappings()
            .Single()
            .Column
            .Name;

    private sealed class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "shipments";

        protected override void ConfigureModule(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OptionalShipment>(ConfigureOptionalShipment);
            modelBuilder.Entity<PlainShipment>();
            modelBuilder.Entity<LabelShipment>(builder => builder.HasOptional(shipment => shipment.Label标签));
            modelBuilder.Entity<PinnedLabelShipment>(ConfigurePinnedLabelShipment);
        }

        private static void ConfigureOptionalShipment(EntityTypeBuilder<OptionalShipment> builder)
        {
            builder.HasOptional(shipment => shipment.Line2Address);
            builder.HasOptional(shipment => shipment.Sha512Hash);
            builder.HasOptional(shipment => shipment.ArchivedOn);
            builder.HasOptional(shipment => shipment.Label标签);
        }

        private static void ConfigurePinnedLabelShipment(EntityTypeBuilder<PinnedLabelShipment> builder) =>
            builder
                .HasOptional(shipment => shipment.Label标签)
                .HasColumnName("label标签");
    }

    private sealed class OptionalShipment(
        Int32? line2Address,
        Int32? sha512Hash,
        DateTimeOffset? archivedOn,
        Int32? label标签)
    {
        private Int32? _line2Address = line2Address;
        private Int32? _sha512Hash = sha512Hash;
        private DateTimeOffset? _archivedOn = archivedOn;
        private Int32? _label标签 = label标签;

        public Guid Id { get; init; }

        public Optional<Int32> Line2Address => _line2Address.AsOptional();

        public Optional<Int32> Sha512Hash => _sha512Hash.AsOptional();

        public Optional<DateTimeOffset> ArchivedOn => _archivedOn.AsOptional();

        public Optional<Int32> Label标签 => _label标签.AsOptional();
    }

    private sealed class PlainShipment
    {
        public Guid Id { get; init; }

        public Int32? Line2Address { get; init; }

        public Int32? Sha512Hash { get; init; }

        public DateTimeOffset? ArchivedOn { get; init; }

        public Int32? Label标签 { get; init; }
    }

    private sealed class TaggedShipment(Int32? tag)
    {
        private Int32? _标签 = tag;

        public Optional<Int32> 标签 => _标签.AsOptional();
    }

    private sealed class LabelShipment(Int32? label标签)
    {
        private Int32? _label标签 = label标签;

        public Guid Id { get; init; }

        public Int32? Label { get; init; }

        public Optional<Int32> Label标签 => _label标签.AsOptional();
    }

    private sealed class PinnedLabelShipment(Int32? label标签)
    {
        private Int32? _label标签 = label标签;

        public Guid Id { get; init; }

        public Int32? Label { get; init; }

        public Optional<Int32> Label标签 => _label标签.AsOptional();
    }
}
