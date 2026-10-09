namespace Tsikhanau.Monolith.Testing.Database;

public sealed record QueueRow(
    Guid Id,
    String Type,
    Int32 Attempts,
    Boolean IsProcessed,
    Boolean HasFailed,
    DateTime NextAttemptOn,
    String Payload);
