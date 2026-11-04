using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Domain.Optionals;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Persistence;

public sealed class OptionalPropertyExtensionsTests
{
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

        var optionalColumn = GetColumnName(dbContext.Model, typeof(OptionalShipment), fieldName);
        var plainColumn = GetColumnName(dbContext.Model, typeof(PlainShipment), propertyName);

        optionalColumn.ShouldBe(plainColumn);
    }

    private static ShipmentsDbContext CreateShipmentsDbContext()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<ShipmentsDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, "Host=localhost", ShipmentsDbContext.SchemaName);
        return new ShipmentsDbContext(dbContextOptionsBuilder.Options);
    }

    private static String? GetColumnName(IModel model, Type entityType, String propertyName) =>
        model
            .FindEntityType(entityType)
            .ShouldNotBeNull()
            .FindProperty(propertyName)
            .ShouldNotBeNull()
            .GetColumnName();

    private sealed class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "shipments";

        protected override void ConfigureModule(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OptionalShipment>(ConfigureOptionalShipment);
            modelBuilder.Entity<PlainShipment>();
        }

        private static void ConfigureOptionalShipment(EntityTypeBuilder<OptionalShipment> builder)
        {
            builder.HasOptional(shipment => shipment.Line2Address);
            builder.HasOptional(shipment => shipment.Sha512Hash);
            builder.HasOptional(shipment => shipment.ArchivedOn);
            builder.HasOptional(shipment => shipment.Label标签);
        }
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
}
