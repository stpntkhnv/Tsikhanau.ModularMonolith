using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal sealed partial class ModuleLogin
{
    public const String Group = "modularmonolith_modules";

    private const String Prefix = "modularmonolith_";
    private const Int32 MaxIdentifierLength = 63;

    private ModuleLogin(String schema, String name, String password)
    {
        Schema = schema;
        Name = name;
        Password = password;
    }

    public String Schema { get; }

    public String Name { get; }

    public String Password { get; }

    public static String BuildName(String schema) => $"{Prefix}{ValidateSchema(schema)}";

    public static ModuleLogin Create(String schema, String adminPassword) =>
        new(schema, BuildName(schema), DerivePassword(BuildName(schema), adminPassword));

    public override String ToString() => Name;

    private static String DerivePassword(String loginName, String adminPassword)
    {
        var key = Encoding.UTF8.GetBytes(adminPassword);
        var message = Encoding.UTF8.GetBytes(loginName);
        return Base64Url.EncodeToString(HMACSHA256.HashData(key, message));
    }

    private static String ValidateSchema(String schema) =>
        IsIdentifier(schema) && Prefix.Length + schema.Length <= MaxIdentifierLength
            ? schema
            : throw new InvalidOperationException($"'{schema}' is not a valid module schema name");

    private static Boolean IsIdentifier(String schema)
    {
        var pattern = SchemaNamePattern();
        return pattern.IsMatch(schema);
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9_]*\z")]
    private static partial Regex SchemaNamePattern();
}
