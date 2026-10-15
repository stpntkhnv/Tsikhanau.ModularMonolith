using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed partial record ModuleName
{
    private const String PascalCaseRule = "must be PascalCase: an ASCII capital letter, then letters and digits";

    private const String TestsSuffixRule = "must not end in Tests: the build turns such a project into a test project";

    private const String ModuleSuffixRule = "must not end in Module: scaffold adds the suffix itself";

    private const String PostgresPrefixRule = "PostgreSQL reserves pg and every name that starts with pg_";

    private const String PostgresSchemaRule = "every PostgreSQL database already has a schema of that name";

    private static readonly String[] PostgresSchemas = ["public", "information_schema"];

    private ModuleName(String name)
    {
        Name = name;
        Schema = name.ToSnakeCase();
    }

    public String Name { get; }

    public String Schema { get; }

    public String Route => Name.ToKebabCase();

    [GeneratedRegex(@"\A[A-Z][A-Za-z0-9]*\z")]
    private static partial Regex PascalCasePattern { get; }

    [GeneratedRegex(@"\A(?!pg(_|\z))[a-z][a-z0-9_]*\z")]
    private static partial Regex SchemaPattern { get; }

    public static Result<ModuleName> Parse(String name, String databaseName) =>
        Result
            .Success(name)
            .Ensure(IsPascalCase, BuildNameError(name, PascalCaseRule))
            .Ensure(IsNotTestProjectName, BuildNameError(name, TestsSuffixRule))
            .Ensure(HasNoModuleSuffix, BuildNameError(name, ModuleSuffixRule))
            .Map(validName => new ModuleName(validName))
            .Ensure(HasSchemaPattern, BuildSchemaError(PostgresPrefixRule))
            .Ensure(IsNotLoginGroup, BuildSchemaError(BuildLoginGroupRule(databaseName)))
            .Ensure(IsNotPostgresSchema, BuildSchemaError(PostgresSchemaRule))
            .Ensure(
                moduleName => FitsRoleNameLength(moduleName, databaseName),
                moduleName => BuildRoleNameLengthError(moduleName, databaseName));

    private static Boolean IsPascalCase(String name) => PascalCasePattern.IsMatch(name);

    private static Boolean IsNotTestProjectName(String name) => !name.EndsWith("Tests", StringComparison.Ordinal);

    private static Boolean HasNoModuleSuffix(String name) => !name.EndsWith("Module", StringComparison.Ordinal);

    private static Boolean HasSchemaPattern(ModuleName moduleName) => SchemaPattern.IsMatch(moduleName.Schema);

    private static Boolean IsNotLoginGroup(ModuleName moduleName) => moduleName.Schema != RoleNames.GroupSuffix;

    private static Boolean IsNotPostgresSchema(ModuleName moduleName) => !PostgresSchemas.Contains(moduleName.Schema);

    private static Boolean FitsRoleNameLength(ModuleName moduleName, String databaseName) =>
        BuildLoginName(moduleName, databaseName).Length <= RoleNames.MaxLength;

    private static String BuildLoginName(ModuleName moduleName, String databaseName) =>
        RoleNames.BuildLogin(databaseName, moduleName.Schema);

    private static String BuildLoginGroupRule(String databaseName) =>
        $"its login would be {RoleNames.BuildGroup(databaseName)}, the group role of the module logins";

    private static Error BuildNameError(String name, String rule) =>
        Error.Failure("scaffold.name", $"module name '{name}' {rule}");

    private static Func<ModuleName, Error> BuildSchemaError(String rule) =>
        moduleName => Error.Failure(
            "scaffold.name",
            $"module name '{moduleName.Name}' gives the schema '{moduleName.Schema}': {rule}");

    private static Error BuildRoleNameLengthError(ModuleName moduleName, String databaseName) =>
        Error.Failure(
            "scaffold.name",
            $"module name '{moduleName.Name}' gives the login {BuildLoginName(moduleName, databaseName)}, "
            + $"longer than the {RoleNames.MaxLength} characters PostgreSQL allows in a role name");
}
