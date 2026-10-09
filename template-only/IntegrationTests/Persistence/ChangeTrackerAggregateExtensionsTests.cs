using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Ids;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Persistence;

public sealed class ChangeTrackerAggregateExtensionsTests
{
    [Fact]
    public void Root_of_a_changed_part_whose_owner_has_a_composite_key_counts_as_changed()
    {
        using var dbContext = CreateOrdersDbContext();
        var firstOrder = Order.Create();
        var secondOrder = Order.Create();
        dbContext.AttachRange(firstOrder, secondOrder);

        secondOrder.Annotate("changed");

        var changedRoots = dbContext.ChangeTracker
            .GetChangedAggregateEntries([])
            .Select(entry => entry.Entity)
            .ToList();

        changedRoots.ShouldBe([secondOrder]);
    }

    private static OrdersDbContext CreateOrdersDbContext()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<OrdersDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, "Host=localhost", OrdersDbContext.SchemaName);
        return new OrdersDbContext(dbContextOptionsBuilder.Options);
    }

    private readonly record struct OrderId(Guid Value) : ITypedId<OrderId>
    {
        public static OrderId From(Guid value) => new(value);
    }

    private readonly record struct OrderLineId(Guid Value) : ITypedId<OrderLineId>
    {
        public static OrderLineId From(Guid value) => new(value);
    }

    private sealed class Order : AggregateRoot<OrderId>
    {
        private readonly List<OrderLine> _lines = [];

        private Order(OrderId id) : base(id) { }

        public IReadOnlyList<OrderLine> Lines => _lines;

        public static Order Create()
        {
            var order = new Order(OrderId.From(Guid.CreateVersion7()));
            order._lines.Add(OrderLine.Create(OrderLineId.From(Guid.CreateVersion7())));
            return order;
        }

        public void Annotate(String text) => _lines[0].Note.Change(text);
    }

    private sealed class OrderLine : Entity<OrderLineId>
    {
        private OrderLine(OrderLineId id) : base(id) { }

        public LineNote Note { get; private set; } = LineNote.Create("first");

        public static OrderLine Create(OrderLineId id) => new(id);
    }

    private sealed class LineNote
    {
        private LineNote(String text) => Text = text;

        public String Text { get; private set; }

        public static LineNote Create(String text) => new(text);

        public void Change(String text) => Text = text;
    }

    private sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> dbContextOptions)
        : ModuleDbContext(dbContextOptions)
    {
        public const String SchemaName = "orders";

        private const String OrderIdProperty = "OrderId";

        protected override String Schema => SchemaName;

        protected override void ConfigureModule(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<Order>()
                .HasKey(order => order.Id);

            modelBuilder
                .Entity<Order>()
                .OwnsMany(order => order.Lines, ConfigureLines);

            modelBuilder
                .Entity<Order>()
                .Navigation(order => order.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }

        private static void ConfigureLines(OwnedNavigationBuilder<Order, OrderLine> builder)
        {
            builder
                .WithOwner()
                .HasForeignKey(OrderIdProperty);

            builder.HasKey(OrderIdProperty, nameof(OrderLine.Id));
            builder.OwnsOne(line => line.Note);
        }
    }
}
