using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Queues;

public sealed class HandlingFailureTests
{
    [Fact]
    public void Event_loop_fails_at_once_because_a_retry_would_loop_again()
    {
        var failure = HandlingFailure.From(UnitOfWorkErrors.EventLoop(roundCount: 10));

        failure.IsPermanent.ShouldBeTrue();
    }

    [Fact]
    public void Other_unexpected_errors_and_conflicts_are_retried()
    {
        var unexpectedFailure = HandlingFailure.From(Error.Unexpected("Tests.Glitch", "Something went wrong"));
        var conflictFailure = HandlingFailure.From(UnitOfWorkErrors.ConcurrencyConflict);

        unexpectedFailure.IsPermanent.ShouldBeFalse();
        conflictFailure.IsPermanent.ShouldBeFalse();
    }
}
