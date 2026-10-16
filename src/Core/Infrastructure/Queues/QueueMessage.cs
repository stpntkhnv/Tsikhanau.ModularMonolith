namespace Tsikhanau.Monolith.Infrastructure.Queues;

public abstract class QueueMessage
{
    internal const String ErasedPayload = "{}";

    public required Guid Id { get; init; }

    public required String Type { get; init; }

    public required String Payload { get; set; }

    public String? TraceParent { get; init; }

    public Int32 Attempts { get; set; }

    public required DateTimeOffset NextAttemptOn { get; set; }

    public Boolean ContainsSecrets { get; init; }

    public String? LastError { get; set; }

    public DateTimeOffset? ProcessedOn { get; set; }

    public DateTimeOffset? FailedOn { get; set; }

    public void EraseSecrets()
    {
        Payload = ErasedPayload;
        LastError = null;
    }
}
