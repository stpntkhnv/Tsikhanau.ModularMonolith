using System.Reflection;
using System.Runtime.CompilerServices;

namespace ModularMonolith.ArchTests.Support;

public static class Mutability
{
    public static IEnumerable<String> GetPublicSetters(Type type) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => IsWritable(property, allowInit: true))
            .Select(property => $"{type.FullName}.{property.Name}");

    public static IEnumerable<String> GetMutableMembers(Type type, Boolean allowInit) =>
        [.. GetWritableProperties(type, allowInit), .. GetWritableFields(type)];

    private static IEnumerable<String> GetWritableProperties(Type type, Boolean allowInit) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => IsWritable(property, allowInit))
            .Select(property => $"{type.FullName}.{property.Name}");

    private static IEnumerable<String> GetWritableFields(Type type) =>
        type
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(field => !field.IsInitOnly)
            .Select(field => $"{type.FullName}.{field.Name}");

    private static Boolean IsWritable(PropertyInfo property, Boolean allowInit) =>
        property.SetMethod is { IsPublic: true } setter && !(allowInit && IsInitOnly(setter));

    private static Boolean IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(IsExternalInit));
}
