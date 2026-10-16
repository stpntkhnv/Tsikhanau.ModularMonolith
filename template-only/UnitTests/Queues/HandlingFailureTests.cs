using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Queues;

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

    [Fact]
    public void Failed_result_is_recorded_under_its_error_code_with_its_message()
    {
        var failure = HandlingFailure.From(Error.Unexpected("Tests.Glitch", "Something went wrong"));

        failure.Code.ShouldBe("Tests.Glitch");
        failure.Reason.ShouldBe("Tests.Glitch: Something went wrong");
    }

    [Fact]
    public void Unknown_type_is_a_retried_failure_under_its_messaging_code()
    {
        var failure = HandlingFailure.From(MessagingErrors.UnknownType("tests.newer"));

        failure.Code.ShouldBe("Messaging.UnknownType");
        failure.IsPermanent.ShouldBeFalse();
    }

    [Fact]
    public void Failures_of_the_handler_itself_have_stable_codes()
    {
        var exception = new InvalidOperationException("boom");

        String[] codes =
        [
            HandlingFailure.From(exception).Code,
            HandlingFailure.TimedOut(TimeSpan.FromMinutes(1), exception).Code,
            HandlingFailure.Abandoned(attempts: 8).Code,
            HandlingFailure.ChangedStateOutsideTransaction.Code,
        ];

        codes.ShouldBe(
        [
            "Handler.Exception",
            "Handler.Timeout",
            "Handler.Abandoned",
            "Handler.ChangedStateOutsideTransaction",
        ]);
    }

    [Fact]
    public void Reason_puts_the_code_before_the_text_of_the_failure()
    {
        var exceptionFailure = HandlingFailure.From(new InvalidOperationException("boom"));
        var timeoutFailure = HandlingFailure.TimedOut(TimeSpan.FromMinutes(1), new OperationCanceledException());
        var abandonedFailure = HandlingFailure.Abandoned(attempts: 8);

        exceptionFailure.Reason.ShouldBe("Handler.Exception: boom");
        timeoutFailure.Reason.ShouldBe("Handler.Timeout: Timed out after 00:01:00");
        abandonedFailure.Reason.ShouldBe("Handler.Abandoned: Gave up after 7 attempts, the last did not finish");
    }

    [Fact]
    public void Code_and_exception_type_name_the_failure_without_its_text()
    {
        var exceptionFailure = HandlingFailure.From(new InvalidOperationException("s3cret"));
        var resultFailure = HandlingFailure.From(Error.Unexpected("Tests.Glitch", "s3cret"));

        exceptionFailure.CodeAndExceptionType.ShouldBe("Handler.Exception (InvalidOperationException)");
        resultFailure.CodeAndExceptionType.ShouldBe("Tests.Glitch");
    }
}
