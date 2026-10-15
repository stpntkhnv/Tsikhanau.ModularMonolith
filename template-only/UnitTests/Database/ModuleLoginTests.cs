using System.Buffers.Text;
using System.Security.Cryptography;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ModuleLoginTests
{
    private const String OtherLoginKey = "YW5vdGhlci1tb25vbGl0aC10ZXN0LWxvZ2luLWtleSE=";

    private static readonly ModuleLoginNames LoginNames = new("monolith");

    [Fact]
    public void Login_is_named_after_the_database_and_the_schema()
    {
        var login = ModuleLogin.Create(LoginNames, "ping_echoes", TestSettings.LoginKey);

        login.Name.ShouldBe("monolith__ping_echoes");
        login.Schema.ShouldBe("ping_echoes");
    }

    [Fact]
    public void Password_is_the_hmac_of_the_login_name_under_the_decoded_login_key()
    {
        var expectedPassword = Base64Url.EncodeToString(
            HMACSHA256.HashData(Convert.FromBase64String(TestSettings.LoginKey), "monolith__pings"u8));

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

        login.Password.ShouldBe(expectedPassword);
    }

    [Fact]
    public void Password_is_stable_for_one_login_key_and_changes_with_it()
    {
        var firstLogin = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);
        var recreatedLogin = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);
        var rotatedLogin = ModuleLogin.Create(LoginNames, "pings", OtherLoginKey);
        var otherSchemaLogin = ModuleLogin.Create(LoginNames, "pongs", TestSettings.LoginKey);

        firstLogin.Password.ShouldBe(recreatedLogin.Password);
        firstLogin.Password.ShouldNotBe(rotatedLogin.Password);
        firstLogin.Password.ShouldNotBe(otherSchemaLogin.Password);
        firstLogin.Password.ShouldMatch("^[A-Za-z0-9_-]{43}$");
    }

    [Fact]
    public void Text_form_never_shows_the_password()
    {
        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

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
        Should.Throw<InvalidOperationException>(() => ModuleLogin.Create(LoginNames, schema, TestSettings.LoginKey));
}
