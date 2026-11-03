using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Tsikhanau.Monolith.Infrastructure.Database.Logins;

internal sealed class ModuleLogin
{
    private ModuleLogin(String schema, String name, String password, String applicationName)
    {
        Schema = schema;
        Name = name;
        Password = password;
        ApplicationName = applicationName;
    }

    public String Schema { get; }

    public String Name { get; }

    public String Password { get; }

    public String ApplicationName { get; }

    public static ModuleLogin Create(ModuleLoginNames loginNames, String schema, String loginKey)
    {
        var name = loginNames.BuildLoginName(schema);
        return new ModuleLogin(schema, name, DerivePassword(name, loginKey), loginNames.BuildApplicationName(schema));
    }

    public override String ToString() => Name;

    private static String DerivePassword(String loginName, String loginKey)
    {
        var key = Convert.FromBase64String(loginKey);
        var message = Encoding.UTF8.GetBytes(loginName);
        return Base64Url.EncodeToString(HMACSHA256.HashData(key, message));
    }
}
