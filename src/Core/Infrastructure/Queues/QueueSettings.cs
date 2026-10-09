using System.ComponentModel.DataAnnotations;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

public sealed record QueueSettings
{
    public const String Section = "ModularMonolith:Queues";

    public Boolean RunInBackground { get; init; } = true;

    [Range(typeof(TimeSpan), "00:00:00.001", "01:00:00")]
    public required TimeSpan PollInterval { get; init; }

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public required TimeSpan HandlerTimeout { get; init; }

    [Range(1, 100)]
    public required Int32 ClaimBatchSize { get; init; }
}
