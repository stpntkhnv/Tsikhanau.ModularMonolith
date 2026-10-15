using System.Text.RegularExpressions;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed partial class ModuleLoginNames
{
    private const String Separator = "__";

    private const String GroupSuffix = "modules";

    private const Int32 MaxRoleNameLength = 63;

    private const String ConnectionStringSetting = $"ConnectionStrings:{DatabaseConnectionStrings.Name}";

    private const String DatabaseNameRule =
        $"cannot name the module roles <database>{Separator}<schema>: it must be groups of a-z and 0-9 joined by "
        + "single underscores, and be neither pg nor start with pg_, which PostgreSQL reserves for its own roles";

    private const String SchemaRule =
        "is not a valid module schema name: it must start with a letter, hold only a-z, 0-9 and _, "
        + "and be neither pg nor start with pg_, which PostgreSQL reserves";

    private const String PostgresSchemaProblem = "every PostgreSQL database already has a schema of that name";

    private static readonly String RoleNameLimit =
        $"more than the {MaxRoleNameLength} PostgreSQL allows in a role name";

    private static readonly String[] PostgresSchemas = ["public", "information_schema"];

    private readonly String _databaseName;

    public ModuleLoginNames(String databaseName)
    {
        _databaseName = ValidateDatabaseName(databaseName);
        Group = ValidateGroupLength(BuildRoleName(GroupSuffix));
    }

    public String Group { get; }

    [GeneratedRegex(@"\A(?!pg(_|\z))[a-z0-9]+(_[a-z0-9]+)*\z")]
    private static partial Regex DatabaseNamePattern { get; }

    [GeneratedRegex(@"\A(?!pg(_|\z))[a-z][a-z0-9_]*\z")]
    private static partial Regex SchemaPattern { get; }

    public String BuildLoginName(String schema) => ValidateLoginLength(schema, BuildRoleName(ValidateSchema(schema)));

    public String BuildApplicationName(String schema) => $"{_databaseName}-{schema}";

    private static String ValidateDatabaseName(String databaseName) =>
        DatabaseNamePattern.IsMatch(databaseName)
            ? databaseName
            : throw new InvalidOperationException(
                $"The database name '{databaseName}' in {ConnectionStringSetting} {DatabaseNameRule}");

    private String BuildRoleName(String name) => $"{_databaseName}{Separator}{name}";

    private String ValidateGroupLength(String group) =>
        group.Length <= MaxRoleNameLength
            ? group
            : throw new InvalidOperationException(
                $"The database name '{_databaseName}' makes the group role {group} {group.Length} characters long, "
                + $"{RoleNameLimit}: shorten the database name to at most "
                + $"{MaxRoleNameLength - Separator.Length - GroupSuffix.Length} characters");

    private String ValidateSchema(String schema)
    {
        if (schema == GroupSuffix)
        {
            throw new InvalidOperationException(
                $"Module schema '{schema}' is reserved: its login would be {Group}, "
                + "the group role of the module logins");
        }

        if (PostgresSchemas.Contains(schema))
        {
            throw new InvalidOperationException($"Module schema '{schema}' is reserved: {PostgresSchemaProblem}");
        }

        return SchemaPattern.IsMatch(schema)
            ? schema
            : throw new InvalidOperationException($"'{schema}' {SchemaRule}");
    }

    private String ValidateLoginLength(String schema, String login) =>
        login.Length <= MaxRoleNameLength
            ? login
            : throw new InvalidOperationException(
                $"Module schema '{schema}' makes the login {login} {login.Length} characters long, "
                + $"{RoleNameLimit}: shorten the module schema or the database name '{_databaseName}'");
}
