using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ModuleLoginNamesTests
{
    private static readonly ModuleLoginNames LoginNames = new("monolith");

    private static readonly String LoginPrefixOf30Characters = new('p', 30);

    [Fact]
    public void Group_is_named_after_the_prefix()
    {
        var group = LoginNames.Group;

        group.ShouldBe("monolith_modules");
    }

    [Fact]
    public void Login_name_joins_the_prefix_and_the_schema_with_an_underscore()
    {
        var loginName = LoginNames.BuildLoginName("pings");

        loginName.ShouldBe("monolith_pings");
    }

    [Fact]
    public void Application_name_joins_the_prefix_and_the_schema_with_a_hyphen()
    {
        var applicationName = LoginNames.BuildApplicationName("pings");

        applicationName.ShouldBe("monolith-pings");
    }

    [Fact]
    public void Schema_whose_login_would_be_the_group_is_refused()
    {
        var exception = Should.Throw<InvalidOperationException>(() => LoginNames.BuildLoginName("modules"));

        exception.Message.ShouldBe(
            "Module schema 'modules' is reserved: its login would be monolith_modules, "
            + "the group role of the module logins");
    }

    [Fact]
    public void Login_name_of_63_characters_is_accepted()
    {
        var loginNames = new ModuleLoginNames(LoginPrefixOf30Characters);

        var loginName = loginNames.BuildLoginName(new String('s', 32));

        loginName.Length.ShouldBe(63);
    }

    [Fact]
    public void Schema_that_makes_the_login_longer_than_63_characters_is_refused()
    {
        var loginNames = new ModuleLoginNames(LoginPrefixOf30Characters);
        var schema = new String('s', 33);

        var exception = Should.Throw<InvalidOperationException>(() => loginNames.BuildLoginName(schema));

        exception.Message.ShouldBe(
            $"'{schema}' is not a valid module schema name: it must start with a letter, hold only a-z, 0-9 and _, "
            + $"and its login {LoginPrefixOf30Characters}_{schema} must fit in 63 characters");
    }
}
