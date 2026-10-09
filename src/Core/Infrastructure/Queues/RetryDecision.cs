namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal readonly record struct RetryDecision(Int32 Attempts, DateTimeOffset NextAttemptOn, DateTimeOffset? FailedOn)
{
    public Boolean HasGivenUp => FailedOn.HasValue;
}
