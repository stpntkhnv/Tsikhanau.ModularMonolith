namespace Acme.Modules.Bad.Infrastructure.Persistence;

internal static class CrateSql
{
    public const String All = "SELECT * FROM bad.crates";

    public const String AllWithCount = "SELECT count(*), c.* FROM bad.crates c";
}
