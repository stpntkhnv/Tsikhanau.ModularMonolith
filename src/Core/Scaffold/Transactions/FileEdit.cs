namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed record FileEdit(String Path, Byte[] Original, Byte[] Updated);
