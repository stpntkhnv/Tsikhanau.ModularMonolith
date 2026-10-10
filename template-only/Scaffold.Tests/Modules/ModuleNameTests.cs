using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class ModuleNameTests
{
    private const String LoginPrefix = "acme_shop";

    private const String PostgresReservesRule = "PostgreSQL reserves pg and every name that starts with pg_";

    private const String PostgresSchemaRule = "every PostgreSQL database already has a schema of that name";

    private const String LongestName = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const String TooLongName = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData("Notes", "notes", "notes")]
    [InlineData("OrderLines", "order_lines", "order-lines")]
    [InlineData("HTTPLogs", "http_logs", "http-logs")]
    [InlineData("Version2Items", "version2items", "version2-items")]
    [InlineData("Pgadmin", "pgadmin", "pgadmin")]
    public void Valid_name_gives_a_snake_case_schema_and_a_kebab_route(String name, String schema, String route)
    {
        var names = ModuleName
            .Parse(name, LoginPrefix)
            .Map(moduleName => (moduleName.Name, moduleName.Schema, moduleName.Route));

        names.ShouldBe(Result.Success((name, schema, route)));
    }

    [Theory]
    [InlineData("notes", "must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData("2Notes", "must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData("Order_Lines", "must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData("Notés", "must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData("", "must be PascalCase: an ASCII capital letter, then letters and digits")]
    [InlineData("LoadTests", "must not end in Tests: the build turns such a project into a test project")]
    [InlineData("NotesModule", "must not end in Module: scaffold adds the suffix itself")]
    public void Name_that_breaks_a_naming_rule_is_refused_with_the_rule(String name, String rule)
    {
        var message = ReadMessage(ModuleName.Parse(name, LoginPrefix));

        message.ShouldBe($"module name '{name}' {rule}");
    }

    [Theory]
    [InlineData("Pg", "pg", PostgresReservesRule)]
    [InlineData("PgStats", "pg_stats", PostgresReservesRule)]
    [InlineData("Modules", "modules", "its login would be acme_shop_modules, the group role of the module logins")]
    [InlineData("Public", "public", PostgresSchemaRule)]
    [InlineData("InformationSchema", "information_schema", PostgresSchemaRule)]
    public void Name_whose_schema_breaks_a_rule_is_refused_with_the_rule(String name, String schema, String rule)
    {
        var message = ReadMessage(ModuleName.Parse(name, LoginPrefix));

        message.ShouldBe($"module name '{name}' gives the schema '{schema}': {rule}");
    }

    [Fact]
    public void Login_of_63_characters_fits_and_one_of_64_is_refused()
    {
        var longest = ModuleName
            .Parse(LongestName, LoginPrefix)
            .Map(moduleName => moduleName.Schema.Length);

        var message = ReadMessage(ModuleName.Parse(TooLongName, LoginPrefix));

        longest.ShouldBe(Result.Success(63 - "acme_shop_".Length));
        message.ShouldBe(
            $"module name '{TooLongName}' gives the login acme_shop_{TooLongName.ToLowerInvariant()}, "
            + "longer than the 63 characters PostgreSQL allows in a role name");
    }

    [Theory]
    [InlineData("Notes")]
    [InlineData("OrderLines")]
    [InlineData("Pg")]
    [InlineData("PgStats")]
    [InlineData("Pgadmin")]
    [InlineData("Modules")]
    [InlineData("Public")]
    [InlineData("InformationSchema")]
    [InlineData(LongestName)]
    [InlineData(TooLongName)]
    public void Scaffold_accepts_exactly_the_schemas_the_core_accepts(String name)
    {
        var moduleName = ModuleName.Parse(name, LoginPrefix);
        var coreAccepts = AcceptsInCore(name.ToSnakeCase());

        moduleName.IsSuccess.ShouldBe(coreAccepts, ReadMessage(moduleName));
    }

    private static Boolean AcceptsInCore(String schema)
    {
        try
        {
            _ = new ModuleLoginNames(LoginPrefix).BuildLoginName(schema);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static String ReadMessage(Result<ModuleName> moduleName) =>
        moduleName.Match(_ => "success", error => error.Message);
}
