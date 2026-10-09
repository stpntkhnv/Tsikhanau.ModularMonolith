using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ModuleLoginTests
{
    private static readonly ModuleLoginNames LoginNames = new("monolith");

    [Fact]
    public void Login_is_named_after_the_prefix_and_the_schema()
    {
        var login = ModuleLogin.Create(LoginNames, "ping_echoes", "admin");

        login.Name.ShouldBe("monolith_ping_echoes");
        login.Schema.ShouldBe("ping_echoes");
    }

    [Fact]
    public void Password_is_stable_for_one_admin_password_and_changes_with_it()
    {
        var firstLogin = ModuleLogin.Create(LoginNames, "pings", "admin");
        var recreatedLogin = ModuleLogin.Create(LoginNames, "pings", "admin");
        var rotatedLogin = ModuleLogin.Create(LoginNames, "pings", "rotated");
        var otherSchemaLogin = ModuleLogin.Create(LoginNames, "pongs", "admin");

        firstLogin.Password.ShouldBe(recreatedLogin.Password);
        firstLogin.Password.ShouldNotBe(rotatedLogin.Password);
        firstLogin.Password.ShouldNotBe(otherSchemaLogin.Password);
        firstLogin.Password.ShouldMatch("^[A-Za-z0-9_-]{43}$");
    }

    [Fact]
    public void Text_form_never_shows_the_password()
    {
        var login = ModuleLogin.Create(LoginNames, "pings", "admin");

        var text = login.ToString();

        text.ShouldNotContain(login.Password);
    }

    [Theory]
    [InlineData("Pings")]
    [InlineData("ping-echoes")]
    [InlineData("1pings")]
    [InlineData("pings;drop")]
    [InlineData("pings\n")]
    [InlineData("")]
    public void Invalid_schema_name_is_refused(String schema) =>
        Should.Throw<InvalidOperationException>(() => ModuleLogin.Create(LoginNames, schema, "admin"));
}
