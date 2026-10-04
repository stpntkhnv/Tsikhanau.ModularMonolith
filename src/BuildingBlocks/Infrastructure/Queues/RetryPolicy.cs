namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

internal static class RetryPolicy
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
    ];

    public static Int32 MaxAttempts => Backoff.Length + 1;

    public static Boolean HasRunOut(Int32 attempts) => attempts > MaxAttempts;

    public static RetryDecision Decide(Int32 attempts, HandlingFailure failure, DateTimeOffset failedOn) =>
        failure.IsPermanent || attempts >= MaxAttempts
            ? new RetryDecision(attempts, NextAttemptOn: failedOn, FailedOn: failedOn)
            : new RetryDecision(attempts, NextAttemptOn: failedOn + GetPauseAfter(attempts), FailedOn: null);

    private static TimeSpan GetPauseAfter(Int32 attempt) => Backoff[attempt - 1];
}
