namespace ModularMonolith.Testing.Queues;

public sealed record UnsettledMessage(String Module, String Table, String Type, Int32 Attempts, String LastError)
{
    public override String ToString() => $"{Module} {Table} {Type} after {Attempts} attempts: {LastError}";
}
