namespace Tsikhanau.Monolith.Scaffold.Apps;

internal static class RoleNames
{
    public const Int32 MaxLength = 63;

    public const String GroupSuffix = "modules";

    public static String BuildLogin(String databaseName, String schema) => $"{databaseName}__{schema}";

    public static String BuildGroup(String databaseName) => BuildLogin(databaseName, GroupSuffix);
}
