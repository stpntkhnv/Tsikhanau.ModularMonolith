using ModularMonolith.Shared.Collections;

namespace ModularMonolith.Shared.Tests.Collections;

public sealed class EnumerableExtensionsTests
{
    [Fact]
    public void ForEach_runs_the_action_for_every_item_in_order()
    {
        var visitedItems = new List<Int32>();

        new[] { 3, 1, 2 }.ForEach(visitedItems.Add);

        visitedItems.ShouldBe([3, 1, 2]);
    }
}
