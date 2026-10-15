using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ModuleLoginNamesTests
{
    private const String DatabaseNameRule =
        "cannot name the module roles <database>__<schema>: it must be groups of a-z and 0-9 joined by single "
        + "underscores, and be neither pg nor start with pg_, which PostgreSQL reserves for its own roles";

    private const String SchemaRule =
        "is not a valid module schema name: it must start with a letter, hold only a-z, 0-9 and _, "
        + "and be neither pg nor start with pg_, which PostgreSQL reserves";

    private static readonly ModuleLoginNames LoginNames = new("shop");

    private static readonly String DatabaseNameOf54Characters = new('d', 54);

    private static readonly String DatabaseNameOf55Characters = new('d', 55);

    [Fact]
    public void Group_is_named_after_the_database()
    {
        var group = LoginNames.Group;

        group.ShouldBe("shop__modules");
    }

    [Fact]
    public void Login_name_joins_the_database_and_the_schema_with_two_underscores()
    {
        var loginName = LoginNames.BuildLoginName("pings");

        loginName.ShouldBe("shop__pings");
    }

    [Fact]
    public void Application_name_joins_the_database_and_the_schema_with_a_hyphen()
    {
        var applicationName = LoginNames.BuildApplicationName("pings");

        applicationName.ShouldBe("shop-pings");
    }

    [Fact]
    public void Database_and_schema_that_split_one_name_differently_get_different_logins()
    {
        var firstLoginName = new ModuleLoginNames("a_b").BuildLoginName("c");
        var secondLoginName = new ModuleLoginNames("a").BuildLoginName("b_c");

        firstLoginName.ShouldBe("a_b__c");
        secondLoginName.ShouldBe("a__b_c");
        firstLoginName.ShouldNotBe(secondLoginName);
    }

    [Theory]
    [InlineData("shop__eu")]
    [InlineData("_shop")]
    [InlineData("shop_")]
    [InlineData("Shop")]
    [InlineData("shop-eu")]
    [InlineData("pg")]
    [InlineData("pg_shop")]
    [InlineData("")]
    public void Database_name_that_breaks_the_rule_is_refused_with_the_rule(String databaseName)
    {
        var exception = Should.Throw<InvalidOperationException>(() => new ModuleLoginNames(databaseName));

        exception.Message.ShouldBe(
            $"The database name '{databaseName}' in ConnectionStrings:database {DatabaseNameRule}");
    }

    [Theory]
    [InlineData("pgx", "pgx__pings")]
    [InlineData("p_g", "p_g__pings")]
    [InlineData("1shop", "1shop__pings")]
    [InlineData("shop_2_eu", "shop_2_eu__pings")]
    public void Database_name_that_keeps_the_rule_names_the_logins(String databaseName, String loginName)
    {
        var builtLoginName = new ModuleLoginNames(databaseName).BuildLoginName("pings");

        builtLoginName.ShouldBe(loginName);
    }

    [Fact]
    public void Group_of_63_characters_is_accepted()
    {
        var loginNames = new ModuleLoginNames(DatabaseNameOf54Characters);

        var group = loginNames.Group;

        group.ShouldBe($"{DatabaseNameOf54Characters}__modules");
        group.Length.ShouldBe(63);
    }

    [Fact]
    public void Database_whose_group_would_be_longer_than_63_characters_is_refused_with_a_short_schema()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => new ModuleLoginNames(DatabaseNameOf55Characters).BuildLoginName("foo"));

        exception.Message.ShouldBe(
            $"The database name '{DatabaseNameOf55Characters}' makes the group role "
            + $"{DatabaseNameOf55Characters}__modules 64 characters long, more than the 63 PostgreSQL allows "
            + "in a role name: shorten the database name to at most 54 characters");
    }

    [Fact]
    public void Login_name_of_63_characters_is_accepted()
    {
        var schema = new String('s', 57);

        var loginName = LoginNames.BuildLoginName(schema);

        loginName.ShouldBe($"shop__{schema}");
        loginName.Length.ShouldBe(63);
    }

    [Fact]
    public void Schema_that_makes_the_login_longer_than_63_characters_is_refused()
    {
        var schema = new String('s', 58);

        var exception = Should.Throw<InvalidOperationException>(() => LoginNames.BuildLoginName(schema));

        exception.Message.ShouldBe(
            $"Module schema '{schema}' makes the login shop__{schema} 64 characters long, more than the 63 "
            + "PostgreSQL allows in a role name: shorten the module schema or the database name 'shop'");
    }

    [Fact]
    public void Schema_whose_login_would_be_the_group_is_refused()
    {
        var exception = Should.Throw<InvalidOperationException>(() => LoginNames.BuildLoginName("modules"));

        exception.Message.ShouldBe(
            "Module schema 'modules' is reserved: its login would be shop__modules, "
            + "the group role of the module logins");
    }

    [Theory]
    [InlineData("public")]
    [InlineData("information_schema")]
    public void Schema_every_postgres_database_has_is_refused(String schema)
    {
        var exception = Should.Throw<InvalidOperationException>(() => LoginNames.BuildLoginName(schema));

        exception.Message.ShouldBe(
            $"Module schema '{schema}' is reserved: every PostgreSQL database already has a schema of that name");
    }

    [Theory]
    [InlineData("pg_x")]
    [InlineData("pg")]
    [InlineData("1pings")]
    public void Schema_that_breaks_the_schema_rule_is_refused(String schema)
    {
        var exception = Should.Throw<InvalidOperationException>(() => LoginNames.BuildLoginName(schema));

        exception.Message.ShouldBe($"'{schema}' {SchemaRule}");
    }

    [Theory]
    [InlineData("pgx")]
    [InlineData("p_g")]
    public void Schema_that_only_starts_like_a_reserved_name_is_accepted(String schema)
    {
        var loginName = LoginNames.BuildLoginName(schema);

        loginName.ShouldBe($"shop__{schema}");
    }
}
