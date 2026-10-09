using System.Security.Cryptography;
using System.Text;
using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class ScramPasswordHasherTests
{
    private const String Password = "kH3b9Qv-_x2mT7cLp0aZ4sWn8yRe1uJf6dGi5oVtBqE";
    private const String Shape = @"^SCRAM-SHA-256\$4096:[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+:[A-Za-z0-9+/=]+$";

    [Fact]
    public void Verifier_has_the_postgres_scram_shape_and_never_shows_the_password()
    {
        var verifier = ScramPasswordHasher.Hash(Password);

        verifier.ShouldMatch(Shape);
        verifier.ShouldNotContain(Password);
    }

    [Fact]
    public void Verifier_keys_derive_from_the_password_and_the_verifiers_own_salt()
    {
        var secretParts = ScramSecretParts.Parse(ScramPasswordHasher.Hash(Password));
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(Password),
            secretParts.Salt,
            iterations: 4096,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        var storedKey = SHA256.HashData(HMACSHA256.HashData(saltedPassword, Encoding.UTF8.GetBytes("Client Key")));
        var serverKey = HMACSHA256.HashData(saltedPassword, Encoding.UTF8.GetBytes("Server Key"));

        secretParts.Salt.Length.ShouldBe(16);
        secretParts.StoredKey.ShouldBe(storedKey);
        secretParts.ServerKey.ShouldBe(serverKey);
    }

    [Fact]
    public void Every_verifier_gets_a_fresh_salt()
    {
        var firstSecretParts = ScramSecretParts.Parse(ScramPasswordHasher.Hash(Password));
        var secondSecretParts = ScramSecretParts.Parse(ScramPasswordHasher.Hash(Password));

        firstSecretParts.Salt.ShouldNotBe(secondSecretParts.Salt);
    }

    private sealed record ScramSecretParts(Byte[] Salt, Byte[] StoredKey, Byte[] ServerKey)
    {
        public static ScramSecretParts Parse(String verifier)
        {
            var sections = verifier.Split('$');
            var salt = sections[1].Split(':')[1];
            var keys = sections[2].Split(':');
            return new ScramSecretParts(
                Convert.FromBase64String(salt),
                Convert.FromBase64String(keys[0]),
                Convert.FromBase64String(keys[1]));
        }
    }
}
