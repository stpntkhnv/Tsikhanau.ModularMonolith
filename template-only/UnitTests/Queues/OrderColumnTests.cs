using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class OrderColumnTests
{
    public static TheoryData<Type, String, String> Queues { get; } = new()
    {
        { typeof(OutboxMessage), OutboxMessage.OrderProperty, OutboxMessage.OrderColumn },
        { typeof(InboxMessage), InboxMessage.OrderProperty, InboxMessage.OrderColumn },
        { typeof(InternalCommandMessage), InternalCommandMessage.OrderProperty, InternalCommandMessage.OrderColumn },
    };

    [Theory]
    [MemberData(nameof(Queues))]
    public void Order_column_is_the_mapped_column_of_the_order_property_and_second_in_the_claim_index(
        Type messageType,
        String orderProperty,
        String orderColumn)
    {
        using var dbContext = new PingsDbContextDesignTimeFactory().CreateDbContext([]);
        var messageEntityType = dbContext
            .GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(messageType)
            .ShouldNotBeNull();

        var modelColumn = messageEntityType
            .FindProperty(orderProperty)
            .ShouldNotBeNull()
            .GetColumnName();

        var claimIndexColumns = messageEntityType
            .GetIndexes()
            .Single(IsClaimIndex)
            .Properties
            .Select(property => property.GetColumnName())
            .ToList();

        orderColumn.ShouldBe(modelColumn);
        claimIndexColumns.ShouldBe(["next_attempt_on", orderColumn, "id"]);
    }

    private static Boolean IsClaimIndex(IIndex index) =>
        index.Properties[0].Name == nameof(QueueMessage.NextAttemptOn);
}
