namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class PostgresVersion
{
    public const Int32 MinimumMajorVersion = 17;

    public const Int32 MinimumServerVersionNumber = MinimumMajorVersion * 10_000;

    public static void EnsureSupported(Int32 serverVersionNumber)
    {
        if (serverVersionNumber < MinimumServerVersionNumber)
        {
            throw new InvalidOperationException(
                $"The database server reports server_version_num {serverVersionNumber}; "
                + $"the app needs PostgreSQL {MinimumMajorVersion} or later");
        }
    }
}
