using System.Text.RegularExpressions;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed partial class ModuleLoginNames
{
    public const String IdentifierPattern = @"\A(?!pg(_|\z))[a-z][a-z0-9_]*\z";

    private const String GroupSuffix = "modules";

    private const Int32 MaxIdentifierLength = 63;

    private const String PostgresSchemaProblem = "every PostgreSQL database already has a schema of that name";

    private static readonly String[] PostgresSchemas = ["public", "information_schema"];

    private readonly String _roleNamePrefix;

    private readonly String _applicationNamePrefix;

    public ModuleLoginNames(String loginPrefix)
    {
        _roleNamePrefix = $"{loginPrefix}_";
        _applicationNamePrefix = $"{loginPrefix}-";
        Group = $"{_roleNamePrefix}{GroupSuffix}";
    }

    public String Group { get; }

    public String BuildLoginName(String schema) => $"{_roleNamePrefix}{ValidateSchema(schema)}";

    public String BuildApplicationName(String schema) => $"{_applicationNamePrefix}{schema}";

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

        return IsIdentifier(schema) && _roleNamePrefix.Length + schema.Length <= MaxIdentifierLength
            ? schema
            : throw new InvalidOperationException(
                $"'{schema}' is not a valid module schema name: it must start with a letter, hold only a-z, 0-9 and _, "
                + "be neither pg nor start with pg_, which PostgreSQL reserves, "
                + $"and its login {_roleNamePrefix}{schema} must fit in {MaxIdentifierLength} characters");
    }

    private static Boolean IsIdentifier(String schema)
    {
        var pattern = SchemaNamePattern();
        return pattern.IsMatch(schema);
    }

    [GeneratedRegex(IdentifierPattern)]
    private static partial Regex SchemaNamePattern();
}
