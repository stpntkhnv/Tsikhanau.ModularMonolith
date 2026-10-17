namespace Tsikhanau.Monolith.IntegrationTests.Host;

internal sealed record InboxRow(
    Int32 Attempts,
    DateTime NextAttemptOn,
    DateTime ReceivedOn,
    DateTime? ProcessedOn,
    DateTime? FailedOn,
    String? LastError);
