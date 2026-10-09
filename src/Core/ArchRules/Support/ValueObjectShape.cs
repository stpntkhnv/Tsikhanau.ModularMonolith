using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ValueObjectShape
{
    public static Boolean HasPublicFrom(Type type) =>
        type.GetMethod("From", BindingFlags.Public | BindingFlags.Static) is not null;
}
