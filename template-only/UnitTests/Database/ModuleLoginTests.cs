using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ModuleLoginTests
{
    [Fact]
    public void Login_is_named_after_the_schema()
    {
        var login = ModuleLogin.Create("ping_echoes", "admin");

        login.Name.ShouldBe("modularmonolith_ping_echoes");
        login.Schema.ShouldBe("ping_echoes");
    }

    [Fact]
    public void Password_is_stable_for_one_admin_password_and_changes_with_it()
    {
        var firstLogin = ModuleLogin.Create("pings", "admin");
        var recreatedLogin = ModuleLogin.Create("pings", "admin");
        var rotatedLogin = ModuleLogin.Create("pings", "rotated");
        var otherSchemaLogin = ModuleLogin.Create("pongs", "admin");

        firstLogin.Password.ShouldBe(recreatedLogin.Password);
        firstLogin.Password.ShouldNotBe(rotatedLogin.Password);
        firstLogin.Password.ShouldNotBe(otherSchemaLogin.Password);
        firstLogin.Password.ShouldMatch("^[A-Za-z0-9_-]{43}$");
    }

    [Fact]
    public void Text_form_never_shows_the_password()
    {
        var login = ModuleLogin.Create("pings", "admin");

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
        Should.Throw<InvalidOperationException>(() => ModuleLogin.Create(schema, "admin"));
}
