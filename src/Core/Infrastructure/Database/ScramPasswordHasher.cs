using System.Security.Cryptography;
using System.Text;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class ScramPasswordHasher
{
    private const Int32 Iterations = 4096;
    private const Int32 SaltLength = 16;
    private const Int32 KeyLength = 32;

    public static String Hash(String password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var saltedPassword = DeriveSaltedPassword(password, salt);
        var keys = $"{ToBase64(ComputeStoredKey(saltedPassword))}:{ToBase64(ComputeServerKey(saltedPassword))}";
        return $"SCRAM-SHA-256${Iterations}:{ToBase64(salt)}${keys}";
    }

    private static Byte[] DeriveSaltedPassword(String password, Byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyLength);

    private static Byte[] ComputeStoredKey(Byte[] saltedPassword) =>
        SHA256.HashData(HMACSHA256.HashData(saltedPassword, "Client Key"u8));

    private static Byte[] ComputeServerKey(Byte[] saltedPassword) =>
        HMACSHA256.HashData(saltedPassword, "Server Key"u8);

    private static String ToBase64(Byte[] bytes) => Convert.ToBase64String(bytes);
}
