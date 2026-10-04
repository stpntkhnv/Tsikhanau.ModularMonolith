using System.Reflection;

namespace ModularMonolith.ArchTests.Support;

public static class ValueObjectShape
{
    public static Boolean HasPublicFrom(Type type) =>
        type.GetMethod("From", BindingFlags.Public | BindingFlags.Static) is not null;
}
